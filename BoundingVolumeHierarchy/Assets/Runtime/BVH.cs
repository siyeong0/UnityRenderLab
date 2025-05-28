using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using static BVH;
using static Master;

public class BVH
{
	public readonly List<Node> nodeList;
	public readonly Triangle[] triangles;
	int maxDepth;
	int numTriangles;

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
	};
	public BuildStats buildStats { get; private set; } = new BuildStats();

	public BVH(Mesh mesh, int maxDepth = 16)
	{
		this.maxDepth = maxDepth;
		buildStats.leafCount = 0;
		buildStats.depthMin = int.MaxValue;
		buildStats.depthMax = int.MinValue;
		buildStats.depthMean = 0f;
		buildStats.leafTriMin = int.MaxValue;
		buildStats.leafTriMax = int.MinValue;
		buildStats.leafTriMean = 0f;

		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		{
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
		stopwatch.Stop();

		buildStats.timeMS = stopwatch.ElapsedMilliseconds;
		buildStats.triangles = numTriangles;
		buildStats.nodeCount = nodeList.Count;
		buildStats.depthMean /= buildStats.leafCount;
		buildStats.leafTriMean /= buildStats.leafCount;
	}

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

	private void splitRecursive(Node parent, int depth = 0)
	{
		if (depth == maxDepth || parent.startIndex + 1 == parent.endIndex)
		{
			++buildStats.leafCount;
			buildStats.depthMin = Mathf.Min(buildStats.depthMin, depth);
			buildStats.depthMax = Mathf.Max(buildStats.depthMax, depth);
			buildStats.depthMean += depth;
			int numLeafTriangles = parent.endIndex - parent.startIndex;
			buildStats.leafTriMin = Mathf.Min(buildStats.leafTriMin, numLeafTriangles);
			buildStats.leafTriMax = Mathf.Max(buildStats.leafTriMax, numLeafTriangles);
			buildStats.leafTriMean += numLeafTriangles;
			return;
		}

		// split the bounding box in half along its longest axis
		Vector3 size = parent.boundsMax - parent.boundsMin;
		int splitAxis = size.x > Mathf.Max(size.y, size.z) ? 0 : size.y > size.z ? 1 : 2;
		float splitValue = ((parent.boundsMax + parent.boundsMin) * 0.5f)[splitAxis];

		// reorder triangles
		int indexA = parent.startIndex;
		int indexB = parent.endIndex - 1;
		while (indexA <= indexB)
		{
			Triangle triangle = triangles[indexA];
			Vector3 triCenter = (triangle.vertexA + triangle.vertexB + triangle.vertexC) / 3f;
			if (triCenter[splitAxis] < splitValue)
			{
				++indexA;
			}
			else
			{
				(triangles[indexA], triangles[indexB]) = (triangles[indexB], triangles[indexA]); // swap
				--indexB;
			}
		}

		// add children to node list
		bool bChildA = indexA > parent.startIndex;
		bool bChildB = indexA < parent.endIndex;
		if (bChildA)
		{
			(Vector3 aMin, Vector3 aMax) = calcBounds(triangles, parent.startIndex, indexA);
			nodeList.Add(new Node(aMin, aMax, parent.startIndex, indexA));
			parent.childAIndex = nodeList.Count - 1;
		}
		if (bChildB)
		{
			(Vector3 bMin, Vector3 bMax) = calcBounds(triangles, indexA, parent.endIndex);
			nodeList.Add(new Node(bMin, bMax, indexA, parent.endIndex));
			parent.childBIndex = nodeList.Count - 1;
		}
		// reculively split childres
		if (bChildA) splitRecursive(nodeList[parent.childAIndex], depth + 1);
		if (bChildB) splitRecursive(nodeList[parent.childBIndex], depth + 1);
	}

	private static (Vector3, Vector3) calcBounds(Triangle[] triangles, int startIndex, int endIndex)
	{
		Bounds bounds = new Bounds(triangles[startIndex].vertexA, Vector3.zero);
		for (int i = startIndex; i < endIndex; ++i)
		{
			Triangle triangle = triangles[i];
			bounds.Encapsulate(triangle.vertexA);
			bounds.Encapsulate(triangle.vertexB);
			bounds.Encapsulate(triangle.vertexC);
		}
		return (bounds.min, bounds.max);
	}
}
