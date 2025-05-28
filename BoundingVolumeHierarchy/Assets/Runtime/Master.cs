using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class Master : MonoBehaviour
{
	[SerializeField] GameObject target;
	[SerializeField] int maxDepth = 16;

	[Header("Visualization Settings")]
	[SerializeField] uint visDepth = 0;
	[SerializeField] bool wireFrame = true;
	[SerializeField, Range(0, 1)] float wireFrameAlpha = 0.15f;
	enum EFillMode { LeafOnly, All }
	[SerializeField] EFillMode fillMode = EFillMode.LeafOnly;
	[SerializeField, Range(0, 1)] float boxAlpha = 0.15f;

	Mesh mesh;
	Mesh prevMesh;
	BVH bvh;


	private void Update()
	{
		mesh = target != null ? target.GetComponentInChildren<MeshFilter>()?.sharedMesh : null;

		if (mesh != null && mesh != prevMesh)
		{
			bvh = new BVH(mesh, maxDepth);
			prevMesh = mesh;
		}
	}

	private void OnDrawGizmos()
	{
		if (bvh != null) drawNodeRecursive(bvh.nodeList[0], 0);
	}

	private void drawNodeRecursive(BVH.Node node, int depth = 0)
	{
		if (depth > visDepth || node == null) return;

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
	}
}