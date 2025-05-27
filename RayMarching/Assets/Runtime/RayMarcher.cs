using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayMarcher : MonoBehaviour
{
	[SerializeField] ComputeShader raymarchingShader;

	RenderTexture outputTexture;
	ComputeBuffer shapeBuffer;
	int numShapes = 0;

	Camera currCamera;
	Light lightSource;

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		initFrame();
		updateParameters();

		raymarchingShader.SetTexture(0, "sourceTex", source);
		raymarchingShader.SetTexture(0, "destinationTex", outputTexture);

		raymarchingShader.Dispatch(0, Mathf.CeilToInt(currCamera.pixelWidth / 16f), Mathf.CeilToInt(currCamera.pixelHeight / 16f), 1);

		Graphics.Blit(outputTexture, destination);
	}

	private void initFrame()
	{
		currCamera = Camera.current;
		lightSource = FindFirstObjectByType<Light>();

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

		// init shape buffer
		List<Shape> shapes = new List<Shape>(FindObjectsByType<Shape>(FindObjectsSortMode.None));
		numShapes = shapes.Count;
		shapes.Sort((a, b) => a.operation.CompareTo(b.operation));

		// hierarchically sorted: each parent is followed by its children in order.
		List<Shape> orderedShapes = new List<Shape>();
		foreach (Shape shape in shapes)
		{
			if (shape.transform.parent == null)
			{
				Transform parent = shape.transform;
				orderedShapes.Add(shape);

				for (int c = 0; c < shape.NumChildren; ++c)
				{
					Shape childShape = parent.GetChild(c).GetComponent<Shape>();
					if (childShape != null)
					{
						orderedShapes.Add(childShape);
					}
				}
			}
		}

		ShapeData[] shapeDataArr = new ShapeData[numShapes];
		for (int i = 0; i < numShapes; i++)
		{
			var s = orderedShapes[i];
			shapeDataArr[i] = new ShapeData()
			{
				position = s.Position,
				rotation = s.Rotation,
				scale = s.Scale,
				color = new Vector3(s.color.r, s.color.g, s.color.b),
				shapeType = (int)s.type,
				operation = (int)s.operation,
				smoothness = s.smoothness * 3,
				numChildren = s.NumChildren
			};
		}

		if (shapeBuffer != null) shapeBuffer.Release();
		shapeBuffer = new ComputeBuffer(numShapes, ShapeData.GetSize());
		shapeBuffer.SetData(shapeDataArr);

		raymarchingShader.SetBuffer(0, "shapeBuffer", shapeBuffer);
		raymarchingShader.SetInt("numShapes", numShapes);
	}

	void updateParameters()
	{
		bool bLightDirectional = lightSource.type == LightType.Directional;
		raymarchingShader.SetMatrix("_CameraToWorld", currCamera.cameraToWorldMatrix);
		raymarchingShader.SetMatrix("_CameraInverseProjection", currCamera.projectionMatrix.inverse);
		raymarchingShader.SetVector("_Light", (bLightDirectional) ? lightSource.transform.forward : lightSource.transform.position);
		raymarchingShader.SetBool("isPositionLight", !bLightDirectional);
	}

	private void OnDestroy()
	{
		if (outputTexture != null) outputTexture.Release();
		if (shapeBuffer != null) shapeBuffer.Release();
	}
}
