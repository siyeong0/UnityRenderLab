using UnityEngine;

public struct Triangle
{
	public Vector3 vertexA;
	public Vector3 vertexB;
	public Vector3 vertexC;

	public Vector3 NormalA;
	public Vector3 NormalB;
	public Vector3 NormalC;

	public Triangle(Vector3 vertexA, Vector3 vertexB, Vector3 vertexC)
	{
		this.vertexA = vertexA;
		this.vertexB = vertexB;
		this.vertexC = vertexC;

		Vector3 edge1 = vertexB - vertexA;
		Vector3 edge2 = vertexC - vertexA;
		Vector3 normal = Vector3.Cross(edge1, edge2).normalized;
		this.NormalA = normal;
		this.NormalB = normal;
		this.NormalC = normal;
	}

	public Triangle(Vector3 vertexA, Vector3 vertexB, Vector3 vertexC, Vector3 normalA, Vector3 normalB, Vector3 normalC)
	{
		this.vertexA = vertexA;
		this.vertexB = vertexB;
		this.vertexC = vertexC;

		this.NormalA = normalA;
		this.NormalB = normalB;
		this.NormalC = normalC;
	}

	public static int GetSize()
	{
		return sizeof(float) * 3 * 6; // 3 vertices + 3 normals
	}
}
