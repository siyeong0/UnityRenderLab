using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class Master : MonoBehaviour
{
	[SerializeField] ComputeShader raytracingShader;

	[SerializeField] GameObject target;
	[SerializeField] int maxDepth = 16;
	[SerializeField] int leafThreshold = 4;

	[Header("Visualization Settings")]
	[SerializeField] uint visDepth = 0;
	[SerializeField] bool wireFrame = true;
	[SerializeField, Range(0, 1)] float wireFrameAlpha = 0.15f;
	enum EFillMode { LeafOnly, All }
	[SerializeField] EFillMode fillMode = EFillMode.LeafOnly;
	[SerializeField, Range(0, 1)] float boxAlpha = 0.15f;

	[SerializeField] bool rotateCamera = true;

	Mesh mesh;
	Mesh prevMesh;
	BVH bvh;

	Camera currCamera;
	RenderTexture outputTexture;
	ComputeBuffer triangleBuffer;
	ComputeBuffer bvhNodeBuffer;

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (bvh == null)
		{
			Graphics.Blit(source, destination);
			return;
		}

		initFrame();
		updateParameters();

		raytracingShader.SetTexture(0, "sourceTex", source);
		raytracingShader.SetTexture(0, "destinationTex", outputTexture);

		raytracingShader.Dispatch(0, Mathf.CeilToInt(currCamera.pixelWidth / 16f), Mathf.CeilToInt(currCamera.pixelHeight / 16f), 1);

		Graphics.Blit(outputTexture, destination);
	}

	private void Update()
	{
		mesh = target != null ? target.GetComponentInChildren<MeshFilter>()?.sharedMesh : null;

		if (mesh != null && mesh != prevMesh)
		{
			bvh = new BVH(mesh, maxDepth, leafThreshold);
			prevMesh = mesh;
		}

		if (rotateCamera && Application.isPlaying)
		{
			Camera.main.transform.RotateAround(Vector3.zero, Vector3.up, 20 * Time.deltaTime);
		}
	}

	private void initFrame()
	{
		currCamera = Camera.current;

		// init render target
		if (outputTexture == null ||
			outputTexture.width != currCamera.pixelWidth ||
			outputTexture.height != currCamera.pixelHeight)
		{
			if (outputTexture != null) outputTexture.Release();
			outputTexture = new RenderTexture(currCamera.pixelWidth, currCamera.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			outputTexture.enableRandomWrite = true;
			outputTexture.Create();
		}

		// init triangle buffer
		if (triangleBuffer == null || triangleBuffer.count != bvh.triangles.Length)
		{
			if (triangleBuffer != null) triangleBuffer.Release();
			triangleBuffer = new ComputeBuffer(bvh.triangles.Length, Triangle.GetSize());
			triangleBuffer.SetData(bvh.triangles);

			raytracingShader.SetBuffer(0, "triangles", triangleBuffer);
		}
		// init bvh node buffer
		if (bvhNodeBuffer == null || bvhNodeBuffer.count != bvh.nodeList.Count)
		{
			if (bvhNodeBuffer != null) bvhNodeBuffer.Release();
			bvhNodeBuffer = new ComputeBuffer(bvh.nodeList.Count, BVH.Node.GetSize());
			bvhNodeBuffer.SetData(bvh.nodeList.ToArray());

			raytracingShader.SetBuffer(0, "bvhNodes", bvhNodeBuffer);
		}
	}

	void updateParameters()
	{
		raytracingShader.SetInt("numTriangles", bvh.triangles.Length);
		raytracingShader.SetMatrix("_CameraToWorld", currCamera.cameraToWorldMatrix);
		raytracingShader.SetMatrix("_CameraInverseProjection", currCamera.projectionMatrix.inverse);
	}

	private void OnDrawGizmos()
	{
		if (bvh != null) drawNodeRecursive(bvh.nodeList[0], 0);
	}

	private void drawNodeRecursive(BVH.Node node, int depth = 0)
	{
		if (depth > visDepth) return;

		Color color = Color.HSVToRGB(depth / 6f % 1, 1, 1);
		if (fillMode == EFillMode.All)
		{
			for (int d = 1; d < depth; ++d)
			{
				color = Color.Lerp(color, Color.HSVToRGB((d / 6f) % 1f, 1, 1), 0.05f);
			}
		}

		Color wireColor = color;
		wireColor.a = wireFrameAlpha;
		Color cubeColor = color;
		cubeColor.a = boxAlpha;

		// draw bounding box
		Bounds bounds = new Bounds((node.boundsMax + node.boundsMin) * 0.5f, (node.boundsMax - node.boundsMin));

		Gizmos.matrix = target.transform.localToWorldMatrix;
		if (fillMode == EFillMode.All ||
			fillMode == EFillMode.LeafOnly && depth == visDepth)
		{
			Gizmos.color = cubeColor;
			Gizmos.DrawWireCube(bounds.center, bounds.size);
			Gizmos.color = cubeColor;
			Gizmos.DrawCube(bounds.center, bounds.size);
		}
		else if (wireFrame)
		{
			Gizmos.color = wireColor;
			Gizmos.DrawWireCube(bounds.center, bounds.size);
		}

		// call recursively
		if (node.numTriangles == 0) // is not a leaf node
		{
			drawNodeRecursive(bvh.nodeList[(int)node.startIndex], depth + 1);
			drawNodeRecursive(bvh.nodeList[(int)node.startIndex + 1], depth + 1);
		}
	}

	void OnGUI()
	{
		if (bvh == null) return;
		GUIStyle boldStyle = new GUIStyle(GUI.skin.label);
		boldStyle.fontStyle = FontStyle.Bold;  // 굵게 설정
		boldStyle.fontSize = 20;

		GUI.color = Color.green;
		GUI.Label(new Rect(20, 20, 400, 800),
			"Time (ms): " + bvh.buildStats.timeMS + "\n" +
			"Triangles: " + bvh.buildStats.triangles + "\n" +
			"Node Count: " + bvh.buildStats.nodeCount + "\n" +
			"Leaf Count: " + bvh.buildStats.leafCount + "\n" +
			"Leaf Depth:\n" +
			" - Min: " + bvh.buildStats.depthMin + "\n" +
			" - Max: " + bvh.buildStats.depthMax + "\n" +
			" - Mean: " + bvh.buildStats.depthMean + "\n" +
			"Leaf Triangles:\n" +
			" - Min: " + bvh.buildStats.leafTriMin + "\n" +
			" - Max: " + bvh.buildStats.leafTriMax + "\n" +
			" - Mean: " + bvh.buildStats.leafTriMean + "\n",
			boldStyle);
	}

	private void OnDisable()
	{
		mesh = null;
		prevMesh = null;

		if (outputTexture != null)
		{
			outputTexture.Release();
			outputTexture = null;
		}
		if (triangleBuffer != null)
		{
			triangleBuffer.Release();
			triangleBuffer = null;
		}
		if (bvhNodeBuffer != null)
		{
			bvhNodeBuffer.Release();
			bvhNodeBuffer = null;
		}
	}

	private void OnDestroy()
	{
		if (outputTexture != null) outputTexture.Release();
		if (triangleBuffer != null) triangleBuffer.Release();
		if (bvhNodeBuffer != null) bvhNodeBuffer.Release();
	}
}