using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static UnityEngine.GraphicsBuffer;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracer : MonoBehaviour
{
	[SerializeField] bool useShaderInSceneView;
	[SerializeField] Shader rayTracingShader;
    [SerializeField] Shader accumulateShader;

    [SerializeField] uint maxRayBounceCount = 4;
    [SerializeField] uint numRaysPerPixel = 4;
[System.Serializable]
    public struct EnvironmentSettings
    {
        public Color groundColour;
        public Color skyColourHorizon;
        public Color skyColourZenith;
        public float sunFocus;
        public float sunIntensity;
    }
    [SerializeField] EnvironmentSettings environmentSettings;

    Material rayTracingMaterial;
    Material accumulateMaterial;
    RenderTexture resultTexture;

    ComputeBuffer sphereBuffer;

    int numRenderedFrames = 0;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
        bool isSceneCam = Camera.current.name == "SceneCamera";

        if (isSceneCam)
        {
            if (useShaderInSceneView)
            {
                initFrame();
                Graphics.Blit(null, destination, rayTracingMaterial);
            }
            else
            {
                Graphics.Blit(source, destination); // Draw the unaltered camera render to the screen
            }
        }
		else
		{
            initFrame();
            // Create copy of prev frame
            RenderTexture prevFrameCopy = RenderTexture.GetTemporary(source.width, source.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
            Graphics.Blit(resultTexture, prevFrameCopy);

            // Run the ray tracing shader and draw the result to a temp texture
            rayTracingMaterial.SetInt("frame", numRenderedFrames);
            RenderTexture currentFrame = RenderTexture.GetTemporary(source.width, source.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
            Graphics.Blit(null, currentFrame, rayTracingMaterial);

            // Accumulate
            accumulateMaterial.SetInt("frame", numRenderedFrames);
            accumulateMaterial.SetTexture("_PrevTex", prevFrameCopy);
            Graphics.Blit(currentFrame, resultTexture, accumulateMaterial);

            // Draw result to screen
            Graphics.Blit(resultTexture, destination);

            // Release temps
            RenderTexture.ReleaseTemporary(currentFrame);
            RenderTexture.ReleaseTemporary(prevFrameCopy);
            RenderTexture.ReleaseTemporary(currentFrame);

            numRenderedFrames += Application.isPlaying ? 1 : 0;
        }
    }

    void initFrame()
    {
        // Create materials used in blits
        ShaderHelper.InitMaterial(rayTracingShader, ref rayTracingMaterial);
        ShaderHelper.InitMaterial(accumulateShader, ref accumulateMaterial);
        // Create result render texture
        ShaderHelper.CreateRenderTexture(ref resultTexture, Screen.width, Screen.height, FilterMode.Bilinear, GraphicsFormat.R32G32B32A32_SFloat, "Result");

        // Update data
        updateCameraParams(Camera.current);
		updateShaderParams();
        updateSpheres();

    }

    private void updateCameraParams(Camera camera)
	{
		float planeHegiht = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f;
		float planeWidth = planeHegiht * camera.aspect;

		rayTracingMaterial.SetVector("viewParams", new Vector3(planeWidth, planeHegiht, camera.nearClipPlane));
		rayTracingMaterial.SetMatrix("cameraLocalToWorldMatrix", camera.transform.localToWorldMatrix);
    }

    void updateShaderParams()
    {
        rayTracingMaterial.SetInt("maxRayBounceCount", (int)maxRayBounceCount);
        rayTracingMaterial.SetInt("numRaysPerPixel", (int)numRaysPerPixel);

        rayTracingMaterial.SetColor("groundColor", environmentSettings.groundColour);
        rayTracingMaterial.SetColor("skyColorHorizon", environmentSettings.skyColourHorizon);
        rayTracingMaterial.SetColor("skyColorZenith", environmentSettings.skyColourZenith);
        rayTracingMaterial.SetFloat("sunFocus", environmentSettings.sunFocus);
        rayTracingMaterial.SetFloat("sunIntensity", environmentSettings.sunIntensity);
    }

    private void updateSpheres()
	{
		// Create sphere data from the sphere objects in the scene
		RayTracedSphere[] sphereObjects = FindObjectsByType<RayTracedSphere>(FindObjectsSortMode.None);
		Sphere[] spheres = new Sphere[sphereObjects.Length];

		for (int i = 0; i < sphereObjects.Length; i++)
		{
			spheres[i] = new Sphere()
			{
				position = sphereObjects[i].transform.position,
				radius = sphereObjects[i].transform.localScale.x * 0.5f,
				material = sphereObjects[i].material
			};
		}

		// Create buffer containing all sphere data, and send it to the shader
		ShaderHelper.CreateStructuredBuffer(ref sphereBuffer, spheres);
		rayTracingMaterial.SetBuffer("spheres", sphereBuffer);
		rayTracingMaterial.SetInt("numSpheres", sphereObjects.Length);
	}

	private void OnDestroy()
	{
        sphereBuffer?.Release();
    }
}