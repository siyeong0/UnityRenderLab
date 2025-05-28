
struct Triangle
{
	float3 vertexA, vertexB, vertexC;
	float3 normalA, normalB, normalC;
};

struct BVHNode
{
	float3 boundsMin;
	float3 boundsMax;
	uint startIndex;
	uint numTriangles;
};

struct Ray
{
	float3 origin;
	float3 dir;
	float3 invDir;
};

struct HitInfo
{
	bool bHit;
	float dist;
	float3 hitPoint;
	float3 normal;
	int triIndex;
	float4 color;
};

HitInfo RayTriangle(Ray ray, Triangle tri)
{
	float3 edgeAB = tri.vertexB - tri.vertexA;
	float3 edgeAC = tri.vertexC - tri.vertexA;
	float3 normalVector = cross(edgeAB, edgeAC);
	float3 ao = ray.origin - tri.vertexA;
	float3 dao = cross(ao, ray.dir);

	float determinant = -dot(ray.dir, normalVector);
	float invDet = 1 / determinant;

				// Calculate dst to triangle & barycentric coordinates of intersection point
	float dist = dot(ao, normalVector) * invDet;
	float u = dot(edgeAC, dao) * invDet;
	float v = -dot(edgeAB, dao) * invDet;
	float w = 1 - u - v;

				// Initialize hit info
	HitInfo hitInfo;
	hitInfo.bHit = determinant >= 1E-8 && dist >= 0 && u >= 0 && v >= 0 && w >= 0;
	hitInfo.hitPoint = ray.origin + ray.dir * dist;
	hitInfo.normal = normalize(tri.normalA * w + tri.normalB * u + tri.normalC * v);
	hitInfo.dist = dist;
	return hitInfo;
}

float RayBoundingBoxDst(Ray ray, float3 boxMin, float3 boxMax)
{
	float3 tMin = (boxMin - ray.origin) * ray.invDir;
	float3 tMax = (boxMax - ray.origin) * ray.invDir;
	float3 t1 = min(tMin, tMax);
	float3 t2 = max(tMin, tMax);
	float tNear = max(max(t1.x, t1.y), t1.z);
	float tFar = min(min(t2.x, t2.y), t2.z);

	bool hit = tFar >= tNear && tFar > 0;
	float dist = hit ? tNear > 0 ? tNear : 0 : 1.#INF;
	return dist;
};

float3 HSVtoRGB(float h, float s, float v)
{
	float c = v * s;
	float x = c * (1 - abs(fmod(h * 6.0, 2.0) - 1));
	float m = v - c;

	float3 rgb;

	if (h < 1.0 / 6.0)
		rgb = float3(c, x, 0);
	else if (h < 2.0 / 6.0)
		rgb = float3(x, c, 0);
	else if (h < 3.0 / 6.0)
		rgb = float3(0, c, x);
	else if (h < 4.0 / 6.0)
		rgb = float3(0, x, c);
	else if (h < 5.0 / 6.0)
		rgb = float3(x, 0, c);
	else
		rgb = float3(c, 0, x);

	return rgb + m;
}