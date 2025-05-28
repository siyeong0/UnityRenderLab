using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class BVH
{
	public readonly List<Node> nodeList;
	public readonly Triangle[] triangles;
	int maxDepth;
	int numTriangles;
	public BuildStats buildStats { get; private set; }

	public BVH(Mesh mesh, int maxDepth = 16)
	{
		buildStats = new BuildStats(this);
		buildStats.StartRecord();
		{
			this.maxDepth = maxDepth;

			// init triangles
			Vector3[] verts = mesh.vertices;
			int[] indices = mesh.triangles;
			Vector3[] normals = mesh.normals;

			numTriangles = indices.Length / 3;
			triangles = new Triangle[numTriangles];
			for (int i = 0; i < numTriangles; ++i)
			{
				int indexA = indices[i * 3 + 0];
				int indexB = indices[i * 3 + 1];
				int indexC = indices[i * 3 + 2];
				triangles[i] = new Triangle(
					verts[indexA], verts[indexB], verts[indexC],
					normals[indexA], normals[indexB], normals[indexC]);
			}

			// build bvh
			nodeList = new List<Node>();
			nodeList.Capacity = 512;

			Node root = new Node(mesh.bounds.min, mesh.bounds.max, 0, numTriangles);
			nodeList.Add(root);
			splitRecursive(root);
		}
		buildStats.EndRecord();
	}

	private void splitRecursive(Node parent, int depth = 0)
	{
		float parentCost = evaluateBoundsCost(parent.boundsMax - parent.boundsMin, parent.endIndex - parent.startIndex);
		(int splitAxis, float splitValue, float cost) = sampleSplit(parent, parent.startIndex, parent.endIndex);

		if (cost >= parentCost || depth == maxDepth)
		{
			buildStats.RecordNode(depth, parent.endIndex - parent.startIndex);
			return;
		}
		else
		{
			// reorder triangles
			int indexA = parent.startIndex;
			int indexB = parent.endIndex - 1;
			Bounds boundsA = new Bounds(triangles[indexA].vertexA, Vector3.zero);
			Bounds boundsB = new Bounds(triangles[indexB].vertexA, Vector3.zero);

			while (indexA <= indexB)
			{
				Triangle triangle = triangles[indexA];
				Vector3 triCenter = (triangle.vertexA + triangle.vertexB + triangle.vertexC) / 3f;
				if (triCenter[splitAxis] < splitValue)
				{
					++indexA;
					encapsulate(ref boundsA, triangle);
				}
				else
				{
					(triangles[indexA], triangles[indexB]) = (triangles[indexB], triangles[indexA]); // swap
					--indexB;
					encapsulate(ref boundsB, triangle);
				}
			}

			// add children to node list
			nodeList.Add(new Node(boundsA.min, boundsA.max, parent.startIndex, indexA));
			parent.childAIndex = nodeList.Count - 1;
			nodeList.Add(new Node(boundsB.min, boundsB.max, indexA, parent.endIndex));
			parent.childBIndex = nodeList.Count - 1;

			// reculively split childres
			splitRecursive(nodeList[parent.childAIndex], depth + 1);
			splitRecursive(nodeList[parent.childBIndex], depth + 1);
		}
	}

	private static void encapsulate(ref Bounds bounds, Triangle tri)
	{
		bounds.Encapsulate(tri.vertexA);
		bounds.Encapsulate(tri.vertexB);
		bounds.Encapsulate(tri.vertexC);
	}

	(int axis, float value, float cost) sampleSplit(Node node, int start, int end)
	{
		if (end - start <= 1) return (0, 0, float.PositiveInfinity); // no split possible

		const int NUM_SAMPLES = 5;
		int bestSplitAxis = 0;
		float bestSplitValue = 0;
		float bestCost = float.MaxValue;

		for (int axis = 0; axis < 3; ++axis)
		{
			for (int i = 0; i < NUM_SAMPLES; ++i)
			{
				float splitT = (i + 1) / (NUM_SAMPLES + 1f);
				float splitValue = Mathf.Lerp(node.boundsMin[axis], node.boundsMax[axis], splitT);
				float cost = evaluateSplit(axis, splitValue, start, end);
				if (cost < bestCost)
				{
					bestCost = cost;
					bestSplitAxis = axis;
					bestSplitValue = splitValue;
				}
			}
		}

		return (bestSplitAxis, bestSplitValue, bestCost);
	}

	float evaluateSplit(int splitAxis, float splitValue, int start, int end)
	{
		Bounds boundA = new Bounds(triangles[start].vertexA, Vector3.zero);
		Bounds boundB = new Bounds(triangles[end - 1].vertexA, Vector3.zero);
		int numTrianglesInA = 0;
		int numTrianglesInB = 0;

		for (int i = start; i < end; ++i)
		{
			Triangle triangle = triangles[i];
			Vector3 triCenter = (triangle.vertexA + triangle.vertexB + triangle.vertexC) / 3f;
			if (triCenter[splitAxis] < splitValue)
			{
				encapsulate(ref boundA, triangle);
				++numTrianglesInA;
			}
			else
			{
				encapsulate(ref boundB, triangle);
				++numTrianglesInB;
			}
		}

		float costA = evaluateBoundsCost(boundA.size, numTrianglesInA);
		float costB = evaluateBoundsCost(boundB.size, numTrianglesInB);
		return (costA + costB);
	}

	static float evaluateBoundsCost(Vector3 size, int numTriangles)
	{
		float halfArea = size.x * size.y + size.x * size.z + size.y * size.z;
		float cost = halfArea * numTriangles;
		return cost == 0 ? float.MaxValue : cost;
	}

	// internal classes
	public class Node
	{
		public Vector3 boundsMin;
		public Vector3 boundsMax;
		public int startIndex;
		public int endIndex;
		public int childAIndex;
		public int childBIndex;

		public Node(Vector3 boundsMin, Vector3 boundsMax, int startIndex = -1, int endIndex = -1)
		{
			this.boundsMin = boundsMin;
			this.boundsMax = boundsMax;
			this.startIndex = startIndex;
			this.endIndex = endIndex;
			childAIndex = -1;
			childBIndex = -1;
		}
	}

	public class BuildStats
	{
		public float timeMS;
		public int triangles;
		public int nodeCount;
		public int leafCount;
		public int depthMin;
		public int depthMax;
		public float depthMean;
		public int leafTriMin;
		public int leafTriMax;
		public float leafTriMean;

		BVH owner;
		public BuildStats(BVH bvh)
		{
			owner = bvh;
		}

		public void RecordNode(int depth, int numTris)
		{
			++leafCount;
			depthMin = Mathf.Min(depthMin, depth);
			depthMax = Mathf.Max(depthMax, depth);
			depthMean += depth;
			leafTriMin = Mathf.Min(leafTriMin, numTris);
			leafTriMax = Mathf.Max(leafTriMax, numTris);
			leafTriMean += numTris;
		}

		Stopwatch stopwatch;
		public void StartRecord()
		{
			stopwatch = new Stopwatch();
			stopwatch.Start();

			leafCount = 0;
			depthMin = int.MaxValue;
			depthMax = int.MinValue;
			depthMean = 0f;
			leafTriMin = int.MaxValue;
			leafTriMax = int.MinValue;
			leafTriMean = 0f;
		}

		public void EndRecord()
		{
			stopwatch.Stop();

			timeMS = stopwatch.ElapsedMilliseconds;
			triangles = owner.numTriangles;
			nodeCount = owner.nodeList.Count;
			depthMean /= leafCount;
			leafTriMean /= leafCount;
		}
	};
}
