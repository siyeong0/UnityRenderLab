using UnityEngine;

public struct ShapeData
{
	public Vector3 position;
	public Vector3 rotation;
	public Vector3 scale;
	public Vector3 color;
	public int shapeType;
	public int operation;
	public float smoothness;
	public int numChildren;

	public static int GetSize()
	{
		return sizeof(float) * 13 + sizeof(int) * 3;
	}
}