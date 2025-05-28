using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class BVH
{
	public readonly List<Node> nodeList;
	public readonly Triangle[] triangles;
	uint numTriangles;
	int maxDepth;
	int leafThreshold;

	Vector3[] triangleCenters;
	BoundingBox[] triangleBounds;

	public BuildStats buildStats { get; private set; }

	public BVH(Mesh mesh, int maxDepth = 16, int leafThreshold = 4)
	{
		buildStats = new BuildStats(this);
		buildStats.StartRecord();
		{
			this.maxDepth = maxDepth;
			this.leafThreshold = leafThreshold;

			// init triangles
			Vector3[] verts = mesh.vertices;
			int[] indices = mesh.triangles;
			Vector3[] normals = mesh.normals;

			numTriangles = (uint)(indices.Length / 3);
			triangles = new Triangle[numTriangles];
			triangleCenters = new Vector3[numTriangles];
			triangleBounds = new BoundingBox[numTriangles];
			for (int i = 0; i < numTriangles; ++i)
			{
				int indexA = indices[i * 3 + 0];
				int indexB = indices[i * 3 + 1];
				int indexC = indices[i * 3 + 2];
				triangles[i] = new Triangle(
					verts[indexA], verts[indexB], verts[indexC],
					normals[indexA], normals[indexB], normals[indexC]);
				// center
				triangleCenters[i] = (verts[indexA] + verts[indexB] + verts[indexC]) / 3f;
				// bounds
				Vector3 min = Vector3.Min(Vector3.Min(triangles[i].vertexA, triangles[i].vertexB), triangles[i].vertexC);
				Vector3 max = Vector3.Max(Vector3.Max(triangles[i].vertexA, triangles[i].vertexB), triangles[i].vertexC);
				triangleBounds[i] = new BoundingBox(min, max);
			}

			// build bvh
			nodeList = new List<Node>();
			nodeList.Capacity = (int)(numTriangles * 2);

			Node root = new Node(new BoundingBox(mesh.bounds.min, mesh.bounds.max));
			nodeList.Add(root);
			splitRecursive(0, 0, 0, numTriangles);
		}
		buildStats.EndRecord();
	}

	private void splitRecursive(int parentIndex, int depth, uint startIndex, uint endIndex)
	{
		Node parent = nodeList[parentIndex];
		int numTris = (int)(endIndex - startIndex);
		float parentCost = evaluateBoundsCost(parent.boundsMax - parent.boundsMin, numTris);
		(int splitAxis, float splitValue, float cost) = sampleSplit(parent, startIndex, endIndex);

		if (cost >= parentCost || depth == maxDepth || numTris <= leafThreshold)
		{
			parent.startIndex = startIndex;
			parent.numTriangles = endIndex - startIndex;
			nodeList[parentIndex] = parent; // update parent node with new start index

			buildStats.RecordNode(depth, numTris);
			return;
		}

		// reorder triangles
		uint indexA = startIndex;
		uint indexB = endIndex - 1;
		while (indexA <= indexB)
		{
			if (triangleCenters[indexA][splitAxis] < splitValue)
			{
				++indexA;
			}
			else
			{
				// swap
				swapElement(triangles, indexA, indexB);
				swapElement(triangleCenters, indexA, indexB);
				swapElement(triangleBounds, indexA, indexB);

				--indexB;
			}
		}
		uint splitIndex = indexA;

		// create bounding boxes for children
		BoundingBox boundsA = new BoundingBox(Vector3.positiveInfinity, Vector3.negativeInfinity);
		BoundingBox boundsB = new BoundingBox(Vector3.positiveInfinity, Vector3.negativeInfinity);
		for (uint i = startIndex; i < splitIndex; ++i)
		{
			boundsA.Encapsulate(triangleBounds[i]);
		}
		for (uint i = splitIndex; i < endIndex; ++i)
		{
			boundsB.Encapsulate(triangleBounds[i]);
		}

		// add children to node list
		nodeList.Add(new Node(boundsA));
		int childAIndex = nodeList.Count - 1;
		nodeList.Add(new Node(boundsB));
		int childBIndex = nodeList.Count - 1;

		parent.startIndex = (uint)childAIndex;
		nodeList[parentIndex] = parent; // update parent node with new start index

		// reculively split childres
		splitRecursive(childAIndex, depth + 1, startIndex, splitIndex);
		splitRecursive(childBIndex, depth + 1, splitIndex, endIndex);

	}

	(int axis, float value, float cost) sampleSplit(Node node, uint start, uint end)
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

	float evaluateSplit(int splitAxis, float splitValue, uint start, uint end)
	{
		BoundingBox boundsA = new BoundingBox(Vector3.positiveInfinity, Vector3.negativeInfinity);
		BoundingBox boundsB = new BoundingBox(Vector3.positiveInfinity, Vector3.negativeInfinity);
		int numTrianglesInA = 0;
		int numTrianglesInB = 0;

		for (uint i = start; i < end; ++i)
		{
			if (triangleCenters[i][splitAxis] < splitValue)
			{
				++numTrianglesInA;
				boundsA.Encapsulate(triangleBounds[i]);
			}
			else
			{
				++numTrianglesInB;
				boundsB.Encapsulate(triangleBounds[i]);
			}
		}

		float costA = evaluateBoundsCost(boundsA.Size, numTrianglesInA);
		float costB = evaluateBoundsCost(boundsB.Size, numTrianglesInB);
		return (costA + costB);
	}

	static float evaluateBoundsCost(Vector3 size, int numTriangles)
	{
		float halfArea = size.x * size.y + size.x * size.z + size.y * size.z;
		float cost = halfArea * numTriangles;
		return Mathf.Max(cost, 1e-6f);
	}

	static void swapElement<T>(T[] array, uint i, uint j)
	{
		T temp = array[i];
		array[i] = array[j];
		array[j] = temp;
	}

	// internal classes
	public struct Node
	{
		public Vector3 boundsMin;
		public Vector3 boundsMax;
		public uint startIndex;
		public uint numTriangles;

		public uint endIndex => startIndex + numTriangles;

		public Node(BoundingBox bounds, uint startIndex = 0, uint numTriangles = 0)
		{
			this.boundsMin = bounds.min;
			this.boundsMax = bounds.max;
			this.startIndex = startIndex;
			this.numTriangles = numTriangles;
		}

		public static int GetSize()
		{
			return sizeof(float) * 3 * 2 + sizeof(uint) * 2;
		}
	}

	public struct BoundingBox
	{
		public Vector3 min;
		public Vector3 max;

		public BoundingBox(Vector3 min, Vector3 max)
		{
			this.min = min;
			this.max = max;
		}
		public Vector3 Center => (min + max) * 0.5f;
		public Vector3 Size => max - min;

		public void Encapsulate(BoundingBox other)
		{
			min = Vector3.Min(min, other.min);
			max = Vector3.Max(max, other.max);
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
			triangles = (int)owner.numTriangles;
			nodeCount = owner.nodeList.Count;
			depthMean /= leafCount;
			leafTriMean /= leafCount;
		}
	};
}
