using UnityEngine;

public class MarCuMeshGeneratorGPU : MonoBehaviour
{
	[SerializeField] ComputeShader marchingCubeComputeShader;
	[SerializeField] Material material;

	[SerializeField] Bounds bounds;
	[SerializeField] float cubeSize = 1f;
	[SerializeField, Range(0, 1)] float surfaceLevel = 0.2f;

	[SerializeField] bool rotateCamera = true;

	[SerializeField, Range(0,1)] float perlinScale = 0.1f;

	PackedBuffer packedGridBuffer;
	Vector3Int gridSize;
	int bitPerCell = 4;

	int marchKernel;
	ComputeBuffer packedGridComputeBuffer;
	ComputeBuffer triangleComputeBuffer;
	int maxTriangles = 999999;

	void Start()
	{
		gridSize = new Vector3Int(
			(int)(bounds.size.x / cubeSize),
			(int)(bounds.size.y / cubeSize),
			(int)(bounds.size.z / cubeSize));
		packedGridBuffer = new PackedBuffer(gridSize.x * gridSize.y * gridSize.z, bitPerCell);
		initBufferPerlinNoise(perlinScale);

		marchKernel = marchingCubeComputeShader.FindKernel("March");

		packedGridComputeBuffer = new ComputeBuffer(packedGridBuffer.RawData.Length, sizeof(uint));
		packedGridComputeBuffer.SetData(packedGridBuffer.RawData);

		triangleComputeBuffer = new ComputeBuffer(maxTriangles, sizeof(float) * 9, ComputeBufferType.Append);
		triangleComputeBuffer.SetCounterValue(0);

		marchingCubeComputeShader.SetBuffer(marchKernel, "packedGridBuffer", packedGridComputeBuffer);
		marchingCubeComputeShader.SetFloat("cubeSize", cubeSize);
		marchingCubeComputeShader.SetInt("bitPerCell", bitPerCell);
		marchingCubeComputeShader.SetVector("boundsMin", -bounds.extents);
		marchingCubeComputeShader.SetInts("gridSize", gridSize.x, gridSize.y, gridSize.z);
		marchingCubeComputeShader.SetFloat("surfaceLevel", surfaceLevel);

		marchingCubeComputeShader.SetBuffer(marchKernel, "triangleBuffer", triangleComputeBuffer);

		material.SetBuffer("triangleBuffer", triangleComputeBuffer);
	}

	void Update()
	{
		if (rotateCamera && Application.isPlaying)
		{
			Camera.main.transform.RotateAround(bounds.center, Vector3.up, 20 * Time.deltaTime);
		}

		if (marchingCubeComputeShader == null || packedGridBuffer == null || packedGridComputeBuffer == null)
			return;

		triangleComputeBuffer.SetCounterValue(0);
		marchingCubeComputeShader.Dispatch(marchKernel, Mathf.CeilToInt(gridSize.x / 8), Mathf.CeilToInt(gridSize.y / 8), Mathf.CeilToInt(gridSize.z / 8));

		ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		ComputeBuffer.CopyCount(triangleComputeBuffer, countBuffer, 0);
		int[] triCount = new int[1];
		countBuffer.GetData(triCount);
		int triangleCount = triCount[0];

		Graphics.DrawProcedural(material, new Bounds(Vector3.zero, Vector3.one * 1000), MeshTopology.Triangles, triangleCount * 3);
	}

	void OnDestroy()
	{
		packedGridComputeBuffer?.Release();
		triangleComputeBuffer?.Release();
	}

	void OnValidate()
	{
		if (!Application.isPlaying)
			Camera.main.transform.position = new Vector3(transform.position.x, transform.position.y, -bounds.extents.z * 3f);

		if (marchingCubeComputeShader == null || packedGridBuffer == null || packedGridComputeBuffer == null)
			return;

		if (Application.isPlaying)
		{
			gridSize = new Vector3Int(
				(int)(bounds.size.x / cubeSize),
				(int)(bounds.size.y / cubeSize),
				(int)(bounds.size.z / cubeSize));
			packedGridBuffer = new PackedBuffer(gridSize.x * gridSize.y * gridSize.z, bitPerCell);
			initBufferPerlinNoise(perlinScale);

			packedGridComputeBuffer.SetData(packedGridBuffer.RawData);
			triangleComputeBuffer.SetCounterValue(0);

			marchingCubeComputeShader.SetBuffer(marchKernel, "packedGridBuffer", packedGridComputeBuffer);
			marchingCubeComputeShader.SetFloat("cubeSize", cubeSize);
			marchingCubeComputeShader.SetInt("bitPerCell", bitPerCell);
			marchingCubeComputeShader.SetVector("boundsMin", -bounds.extents);
			marchingCubeComputeShader.SetInts("gridSize", gridSize.x, gridSize.y, gridSize.z);
			marchingCubeComputeShader.SetFloat("surfaceLevel", surfaceLevel);

			marchingCubeComputeShader.SetBuffer(marchKernel, "triangleBuffer", triangleComputeBuffer);
		}
	}

	private void initBufferRandomNoise()
	{
		for (int x = 1; x < gridSize.x - 1; x++)
		{
			for (int y = 1; y < gridSize.y - 1; y++)
			{
				for (int z = 1; z < gridSize.z - 1; z++)
				{
					packedGridBuffer[cvtGridToBufIdx(x,y,z)] = (uint)Random.Range(0, getCellMaxValue());
				}
			}
		}
	}

	private void initBufferPerlinNoise(float scale = 0.1f)
	{
		for (int x = 1; x < gridSize.x - 1; x++)
		{
			for (int y = 1; y < gridSize.y - 1; y++)
			{
				for (int z = 1; z < gridSize.z - 1; z++)
				{
					float nx = x * scale;
					float ny = y * scale;
					float nz = z * scale;
					float noise = Mathf.PerlinNoise(nx, ny) * Mathf.PerlinNoise(ny, nz);
					float remapped = Mathf.Lerp(0, getCellMaxValue(), noise);

					packedGridBuffer[cvtGridToBufIdx(x, y, z)] = (uint)Mathf.FloorToInt(remapped);
				}
			}
		}
	}

	private int cvtGridToBufIdx(int x, int y, int z)
	{
		return x + y * gridSize.x + z * gridSize.x * gridSize.y;
	}

	private int getCellMaxValue()
	{
		return 1 << bitPerCell;
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.white;
		Gizmos.DrawWireCube(bounds.center, bounds.size);
	}
}
