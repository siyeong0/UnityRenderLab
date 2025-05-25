using UnityEngine;

namespace RayTracing
{
	[System.Serializable]
	public struct Sphere
	{
		public Vector3 position;
		public float radius;
		public RayTracingMaterial material;
	};
}