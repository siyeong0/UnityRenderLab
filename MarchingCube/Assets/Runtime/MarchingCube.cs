using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class MarchingCube : MonoBehaviour
{
	[SerializeField] bool drawCube = true;

	[SerializeField] Bounds bounds;
	[SerializeField] float cubeSize = 1f;
	[SerializeField, Range(-34, 16)] float surfaceLevel = 0.2f;

	[SerializeField] bool rotateCamera = true;

	[Header("Gizmos")]
	[SerializeField] bool drawSphere = true;
	[SerializeField, Range(0, 1)] float sphereSize = 0.1f;

	int[,,] buffer;
	MeshFilter meshFilter;
	MeshRenderer meshRenderer;

	struct Triangle
	{
		public Vector3 a, b, c;
		public Triangle(Vector3 a, Vector3 b, Vector3 c)
		{
			this.a = a;
			this.b = b;
			this.c = c;
		}
	}

	void Update()
	{
		if (rotateCamera && Application.isPlaying)
		{
			Camera.main.transform.RotateAround(bounds.center, Vector3.up, 20 * Time.deltaTime);
		}
	}

	void OnEnable()
	{
		meshFilter = GetComponent<MeshFilter>();
		meshRenderer = GetComponent<MeshRenderer>();
		initBufferPerlinNoise();
	}

	private void OnValidate()
	{
		if (!drawCube)
		{
			if (meshFilter != null) meshFilter.sharedMesh = null;
			return;
		}
		else
		{
			if (buffer != null)	render();
		}
	}

	void render()
	{
		List<Triangle> triangles = triangulation();

		List<Vector3> verts = new List<Vector3>();
		List<int> indices = new List<int>();

		for (int i = 0; i < triangles.Count; i++)
		{
			verts.Add(triangles[i].a);
			verts.Add(triangles[i].b);
			verts.Add(triangles[i].c);
			indices.Add(i * 3);
			indices.Add(i * 3 + 1);
			indices.Add(i * 3 + 2);
		}

		Mesh mesh = new Mesh();
		mesh.SetVertices(verts);
		mesh.SetTriangles(indices, 0);
		mesh.RecalculateNormals();

		meshFilter.sharedMesh = mesh;
	}

	private List<Triangle> triangulation()
	{
		List<Triangle> triangles = new List<Triangle>();

		for (int x = 0; x < buffer.GetLength(0) - 1; x++)
		{
			for (int y = 0; y < buffer.GetLength(1) - 1; y++)
			{
				for (int z = 0; z < buffer.GetLength(2) - 1; z++)
				{
					Vector4[] corners = new Vector4[8]
						{
							GetCubeVert(x    , y    , z    ),
							GetCubeVert(x + 1, y	, z    ),
							GetCubeVert(x + 1, y	, z + 1),
							GetCubeVert(x    , y	, z + 1),
							GetCubeVert(x    , y + 1, z    ),
							GetCubeVert(x + 1, y + 1, z    ),
							GetCubeVert(x + 1, y + 1, z + 1),
							GetCubeVert(x    , y + 1, z + 1)
						};

					uint cubeIndex = 0;
					if (corners[0].w < surfaceLevel) cubeIndex |= 1u;
					if (corners[1].w < surfaceLevel) cubeIndex |= 2u;
					if (corners[2].w < surfaceLevel) cubeIndex |= 4u;
					if (corners[3].w < surfaceLevel) cubeIndex |= 8u;
					if (corners[4].w < surfaceLevel) cubeIndex |= 16u;
					if (corners[5].w < surfaceLevel) cubeIndex |= 32u;
					if (corners[6].w < surfaceLevel) cubeIndex |= 64u;
					if (corners[7].w < surfaceLevel) cubeIndex |= 128u;

					for (int i = 0; MarchTable.triangulation[cubeIndex, i] != -1; i += 3)
					{
						int a0 = MarchTable.cornerIndexAFromEdge[MarchTable.triangulation[cubeIndex, i]];
						int b0 = MarchTable.cornerIndexBFromEdge[MarchTable.triangulation[cubeIndex, i]];

						int a1 = MarchTable.cornerIndexAFromEdge[MarchTable.triangulation[cubeIndex, i + 1]];
						int b1 = MarchTable.cornerIndexBFromEdge[MarchTable.triangulation[cubeIndex, i + 1]];

						int a2 = MarchTable.cornerIndexAFromEdge[MarchTable.triangulation[cubeIndex, i + 2]];
						int b2 = MarchTable.cornerIndexBFromEdge[MarchTable.triangulation[cubeIndex, i + 2]];

						Vector3 vertexA = (corners[a0] + corners[b0]) * 0.5f;
						Vector3 vertexB = (corners[a1] + corners[b1]) * 0.5f;
						Vector3 vertexC = (corners[a2] + corners[b2]) * 0.5f;

						//Vector3 vertexA = interpolateVerts(corners[a0], corners[b0]);
						//Vector3 vertexB = interpolateVerts(corners[a1], corners[b1]);
						//Vector3 vertexC = interpolateVerts(corners[a2], corners[b2]);

						triangles.Add(new Triangle(vertexA, vertexB, vertexC));
					}
				}
			}
		}

		return triangles;
	}

	private Vector4 GetCubeVert(int x, int y, int z)
	{
		Vector3 position = bounds.min + new Vector3(x * cubeSize, y * cubeSize, z * cubeSize);
		return new Vector4(position.x, position.y, position.z, buffer[x, y, z]);
	}

	private Vector3 interpolateVerts(Vector4 v1, Vector4 v2)
	{
		float t = (surfaceLevel - v1.w) / (v2.w - v1.w);
		Vector3 v1xyz = new Vector3(v1.x, v1.y, v1.z);
		Vector3 v2xyz = new Vector3(v2.x, v2.y, v2.z);
		return v1xyz + t * (v2xyz - v1xyz);
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.white;
		Gizmos.DrawWireCube(bounds.center, bounds.size);

		if (drawSphere)
		{
			for (int x = 0; x < buffer.GetLength(0); x++)
			{
				for (int y = 0; y < buffer.GetLength(1); y++)
				{
					for (int z = 0; z < buffer.GetLength(2); z++)
					{
						if (surfaceLevel <= buffer[x, y, z])
						{
							float colorValue = (float)(buffer[x, y, z] + 34) / 50f;
							Gizmos.color = new Color(colorValue, colorValue, colorValue);
							Gizmos.DrawSphere(new Vector3(x * cubeSize, y * cubeSize, z * cubeSize) - bounds.extents, sphereSize);
						}
					}
				}
			}
		}
	}

	private void initBufferRandomNoise()
	{
		buffer = new int[(int)(bounds.size.x / cubeSize + 1), (int)(bounds.size.y / cubeSize + 1), (int)(bounds.size.z / cubeSize + 1)];
		for (int x = 0; x < buffer.GetLength(0); x++)
		{
			for (int y = 0; y < buffer.GetLength(1); y++)
			{
				for (int z = 0; z < buffer.GetLength(2); z++)
				{
					buffer[x, y, z] = Random.Range(-34, 16);
				}
			}
		}
	}

	private void initBufferPerlinNoise(float scale = 0.1f, float threshold = 0.5f)
	{
		buffer = new int[(int)(bounds.size.x / cubeSize + 1), (int)(bounds.size.y / cubeSize + 1), (int)(bounds.size.z / cubeSize + 1)];

		for (int x = 0; x < buffer.GetLength(0); x++)
		{
			for (int y = 0; y < buffer.GetLength(1); y++)
			{
				for (int z = 0; z < buffer.GetLength(2); z++)
				{
					buffer[x, y, z] = -34;
				}
			}
		}

		for (int x = 1; x < buffer.GetLength(0) - 1; x++)
		{
			for (int y = 1; y < buffer.GetLength(1) - 1; y++)
			{
				for (int z = 1; z < buffer.GetLength(2) - 1; z++)
				{
					float nx = x * scale;
					float ny = y * scale;
					float nz = z * scale;
					float noise = Mathf.PerlinNoise(nx, ny) * Mathf.PerlinNoise(ny, nz);
					float remapped = Mathf.Lerp(-34, 16, noise);
					buffer[x, y, z] = Mathf.FloorToInt(remapped);
				}
			}
		}
	}
}
