using System.Collections.Generic;
using UnityEngine;

public class MeshVoxelizationTest : MonoBehaviour
{
	[SerializeField] Mesh mesh;

	[SerializeField, Range(0.1f, 1.0f)] float voxelSize = 0.05f;
	[SerializeField] bool drawAABB = true;

	bool[,,] voxelGrid;

	Material material;
	Mesh voxelMesh;
	Bounds gridBounds;
	List<Matrix4x4> matrices = new List<Matrix4x4>();

	bool bDirty = true;

	private void Start()
	{
		matrices = new List<Matrix4x4>();
		voxelMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
		material = new Material(Shader.Find("Standard"));
		material.enableInstancing = true;
	}

	private void Update()
	{
		if (bDirty)
		{
			bDirty = false;

			matrices.Clear();

			Voxelize();

			Vector3Int gridSize = new Vector3Int();
			gridSize.x = voxelGrid.GetLength(0);
			gridSize.y = voxelGrid.GetLength(1);
			gridSize.z = voxelGrid.GetLength(2);
			for (int x = 0; x < gridSize.x; x++)
			{
				for (int y = 0; y < gridSize.y; y++)
				{
					for (int z = 0; z < gridSize.z; z++)
					{
						if (voxelGrid[x, y, z])
						{
							Vector3 position = gridBounds.min + new Vector3(x, y, z) * voxelSize + Vector3.one * voxelSize * 0.5f;
							Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * voxelSize);
							matrices.Add(matrix);
						}
					}
				}
			}
		}

		int batchSize = 1023;
		for (int i = 0; i < matrices.Count; i += batchSize)
		{
			int count = Mathf.Min(batchSize, matrices.Count - i);
			Graphics.DrawMeshInstanced(voxelMesh, 0, material, matrices.GetRange(i, count));
		}
	}

	private void OnDrawGizmos()
	{
		if (drawAABB)
		{
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(gridBounds.center, gridBounds.size);
		}
	}

	private void OnValidate()
	{
		bDirty = true;
	}

	private void Voxelize()
	{
		Bounds bounds = mesh.bounds;
		Vector3 min = bounds.min;
		Vector3 max = bounds.max;
		Vector3 size = bounds.size;

		Vector3Int gridSize = new Vector3Int((int)(size.x / voxelSize), (int)(size.y / voxelSize), (int)(size.z / voxelSize));
		gridSize.x = gridSize.x == 0 ? 1 : gridSize.x;
		gridSize.y = gridSize.y == 0 ? 1 : gridSize.y;
		gridSize.z = gridSize.z == 0 ? 1 : gridSize.z;
		voxelGrid = new bool[gridSize.x, gridSize.y, gridSize.z];

		Vector3[] vertices = mesh.vertices;
		int[] triangles = mesh.triangles;

		for (int i = 0; i < triangles.Length; i += 3)
		{
			Vector3 vertex0 = vertices[triangles[i]];
			Vector3 vertex1 = vertices[triangles[i + 1]];
			Vector3 vertex2 = vertices[triangles[i + 2]];

			Vector3 norm0 = normalizeToBounds(vertices[triangles[i]], min, size);
			Vector3 norm1 = normalizeToBounds(vertices[triangles[i + 1]], min, size);
			Vector3 norm2 = normalizeToBounds(vertices[triangles[i + 2]], min, size);

			Vector3 v0 = new Vector3(norm0.x * (gridSize.x - 1), norm0.y * (gridSize.y - 1), norm0.z * (gridSize.z - 1));
			Vector3 v1 = new Vector3(norm1.x * (gridSize.x - 1), norm1.y * (gridSize.y - 1), norm1.z * (gridSize.z - 1));
			Vector3 v2 = new Vector3(norm2.x * (gridSize.x - 1), norm2.y * (gridSize.y - 1), norm2.z * (gridSize.z - 1));

			Bounds aabb = new Bounds(v0, Vector3.zero);
			aabb.Encapsulate(v1);
			aabb.Encapsulate(v2);

			Vector3Int triMin = Vector3Int.FloorToInt(aabb.min);
			Vector3Int triMax = Vector3Int.CeilToInt(aabb.max);
			for (int z = triMin.z; z <= triMax.z; z++)
			{
				for (int y = triMin.y; y <= triMax.y; y++)
				{
					for (int x = triMin.x; x <= triMax.x; x++)
					{
						if (x < 0 || x >= gridSize.x ||
							y < 0 || y >= gridSize.y ||
							z < 0 || z >= gridSize.z)
						{
							continue;
						}

						Bounds voxel = new Bounds(new Vector3(x, y, z) + Vector3.one * 0.5f, Vector3.one);
						bool bIntersect = doIntersectBoxAndTri(voxel, v0, v1, v2);
						voxelGrid[x, y, z] |= bIntersect;
					}
				}
			}
		}
	}

	private void DrawArrowHead(Vector3 position, Vector3 direction, float size)
	{
		Vector3 right = Vector3.Cross(direction, Vector3.up);
		if (right == Vector3.zero)
			right = Vector3.Cross(direction, Vector3.forward);
		right.Normalize();

		Vector3 up = Vector3.Cross(right, direction).normalized;

		Vector3 p1 = position;
		Vector3 p2 = position - direction * size + right * size * 0.5f;
		Vector3 p3 = position - direction * size - right * size * 0.5f;

		Gizmos.DrawLine(p1, p2);
		Gizmos.DrawLine(p1, p3);
	}

	private Vector3 normalizeToBounds(Vector3 v, Vector3 min, Vector3 size)
	{
		return new Vector3(
			(v.x - min.x) / size.x,
			(v.y - min.y) / size.y,
			(v.z - min.z) / size.z
		);
	}

	private void rasterizeTriangle(bool[,,] grid, Vector3 v0, Vector3 v1, Vector3 v2)
	{
		Bounds aabb = new Bounds(v0, Vector3.zero);
		aabb.Encapsulate(v1);
		aabb.Encapsulate(v2);

		Vector3Int min = Vector3Int.FloorToInt(aabb.min);
		Vector3Int max = Vector3Int.CeilToInt(aabb.max);

		Vector3Int gridSize = new Vector3Int(
			grid.GetLength(0),
			grid.GetLength(1),
			grid.GetLength(2)
		);
		for (int z = min.z; z <= max.z; z++)
		{
			for (int y = min.y; y <= max.y; y++)
			{
				for (int x = min.x; x <= max.x; x++)
				{
					if (x < 0 || x >= gridSize.x ||
						y < 0 || y >= gridSize.y ||
						z < 0 || z >= gridSize.z)
					{
						continue;
					}

					Bounds voxel = new Bounds(new Vector3(x, y, z) + Vector3.one * 0.5f, Vector3.one);
					grid[x, y, z] |= doIntersectBoxAndTri(voxel, v0, v1, v2);
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
