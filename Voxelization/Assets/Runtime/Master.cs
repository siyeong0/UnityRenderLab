using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class Master : MonoBehaviour
{
	[SerializeField] Mesh mesh;

	[SerializeField, Range(0.1f, 1.0f)] float voxelSize = 0.05f;
	[SerializeField, Range(3, 12)] int maxOctreeDepth = 8;



	private void OnValidate()
	{
		Voxelize();
	}

	private void Voxelize()
	{
		// Assert center of a mesh is (0,0,0)
		float octreeSize = voxelSize * Mathf.Pow(2f, maxOctreeDepth);
		float maxMeshExtent =
			Mathf.Max(
				Mathf.Max(Mathf.Abs(mesh.bounds.min.x), Mathf.Abs(mesh.bounds.min.y), Mathf.Abs(mesh.bounds.min.z)),
				Mathf.Max(Mathf.Abs(mesh.bounds.max.x), Mathf.Abs(mesh.bounds.max.y), Mathf.Abs(mesh.bounds.max.z)));
		int numOctreesPerExtent = (int)Mathf.Ceil(maxMeshExtent / octreeSize);
		float boundingCubeExtent = octreeSize * numOctreesPerExtent;
		int numCellsPerAxis = Mathf.CeilToInt(boundingCubeExtent * 2f / voxelSize);
		int numOctreesPerAxis = numOctreesPerExtent * 2;

		Assert.IsTrue(numCellsPerAxis % 8 == 0);
		int gridBufferSize = (numCellsPerAxis / 8) * (numCellsPerAxis / 8) * (numCellsPerAxis / 8);

		// Precompute the triangles intersecting each octree
		List<int>[,,] interTriListOfOctrees = new List<int>[numOctreesPerAxis, numOctreesPerAxis, numOctreesPerAxis];
		for (int z = 0; z < numOctreesPerAxis; ++z)
		{
			for (int y = 0; y < numOctreesPerAxis; ++y)
			{
				for (int x = 0; x < numOctreesPerAxis; ++x)
				{
					interTriListOfOctrees[x, y, z] = new List<int>();
					Bounds octreeAABB = new Bounds(
						-boundingCubeExtent * Vector3.one + octreeSize * (new Vector3(x, y, z) + 0.5f * Vector3.one),
						octreeSize * Vector3.one);
					for (int triIdx = 0; triIdx < mesh.triangles.Length / 3; ++triIdx)
					{
						Vector3 vertexA = mesh.vertices[3 * triIdx + 0];
						Vector3 vertexB = mesh.vertices[3 * triIdx + 1];
						Vector3 vertexC = mesh.vertices[3 * triIdx + 2];

						if (doIntersectBoxAndTri(octreeAABB, vertexA, vertexB, vertexC))
						{
							interTriListOfOctrees[x, y, z].Append(triIdx);
						}
					}
				}
			}
		}

		// Build octrees
		for (int z = 0; z < numOctreesPerAxis; ++z)
		{
			for (int y = 0; y < numOctreesPerAxis; ++y)
			{
				for (int x = 0; x < numOctreesPerAxis; ++x)
				{

				}
			}
		}
	}

	static private bool doIntersectBoxAndTri(Bounds aabb, Vector3 v0, Vector3 v1, Vector3 v2)
	{
		// Triangle in box space
		v0 -= aabb.center;
		v1 -= aabb.center;
		v2 -= aabb.center;

		// Compute triangle edges
		Vector3 e0 = v1 - v0;
		Vector3 e1 = v2 - v1;
		Vector3 e2 = v0 - v2;

		// 9 Axis tests (cross product of box axes and triangle edges)
		Vector3[] axes = new Vector3[] {
		new Vector3(0, -e0.z, e0.y),
		new Vector3(0, -e1.z, e1.y),
		new Vector3(0, -e2.z, e2.y),
		new Vector3(e0.z, 0, -e0.x),
		new Vector3(e1.z, 0, -e1.x),
		new Vector3(e2.z, 0, -e2.x),
		new Vector3(-e0.y, e0.x, 0),
		new Vector3(-e1.y, e1.x, 0),
		new Vector3(-e2.y, e2.x, 0)
	};

		foreach (Vector3 axis in axes)
		{
			if (!doIntersectAxis(axis, v0, v1, v2, aabb.extents))
			{
				return false;
			}
		}

		// 3 face axes
		for (int i = 0; i < 3; i++)
		{
			if (!doIntersectAxis(Vector3.right * (i == 0 ? 1 : 0) + Vector3.up * (i == 1 ? 1 : 0) + Vector3.forward * (i == 2 ? 1 : 0), v0, v1, v2, aabb.extents))
			{
				return false;
			}
		}

		// Triangle normal test
		Vector3 normal = Vector3.Cross(e0, e1);
		if (!doIntersectAxis(normal, v0, v1, v2, aabb.extents))
		{
			return false;
		}

		return true;
	}

	static private bool doIntersectAxis(Vector3 axis, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 halfSize)
	{
		// Skip degenerate axis
		if (axis == Vector3.zero) return true;

		// Project triangle onto axis
		float p0 = Vector3.Dot(v0, axis);
		float p1 = Vector3.Dot(v1, axis);
		float p2 = Vector3.Dot(v2, axis);

		float triMin = Mathf.Min(p0, Mathf.Min(p1, p2));
		float triMax = Mathf.Max(p0, Mathf.Max(p1, p2));

		// Project box onto axis
		float r = halfSize.x * Mathf.Abs(axis.x) +
				  halfSize.y * Mathf.Abs(axis.y) +
				  halfSize.z * Mathf.Abs(axis.z);

		return !(triMin > r || triMax < -r);
	}
}