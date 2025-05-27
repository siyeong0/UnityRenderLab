using UnityEngine;

public class Shape : MonoBehaviour
{
	public enum EType { Sphere, Cube, Torus, Prism, Cylinder };
	public enum EOperation 
	{ 
		Union, Subtract, Intersect,
		SmoothUnion, SmoothSubtract, SmoothIntersect,
	};

	public EType type = EType.Sphere;
	public EOperation operation = EOperation.Union;
	public Color color = Color.white;
	[Range(0, 1)] public float smoothness = 0;

	public int NumChildren
	{
		get
		{
			int count = 0;
			foreach (Transform child in transform)
			{
				if (child.GetComponent<Shape>() != null) count++;
			}
			return count;
		}
	}

	public Vector3 Position
	{
		get => transform.position;
		set => transform.position = value;
	}

	public Vector3 Rotation
	{
		get => transform.eulerAngles;
		set => transform.eulerAngles = value;
	}

	public Vector3 Scale
	{
		get
		{
			Vector3 parentScale = Vector3.one;
			if (transform.parent != null && transform.parent.GetComponent<Shape>() != null)
			{
				parentScale = transform.parent.GetComponent<Shape>().Scale;
			}
			return Vector3.Scale(transform.localScale, parentScale);
		}
	}
}
