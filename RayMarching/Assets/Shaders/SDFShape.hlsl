static const float MAX_DISTANCE = 100.0;
static const float EPSILON = 0.001;
static const float PI = 3.14159265358979323846;
static const float DEG_TO_RAD = PI / 180.0;
static const float RAD_TO_DEG = 180.0 / PI;

struct Shape
{
	float3 position;
	float3 rotation;
	float3 size;
	float3 color;
	int shapeType;
	int operation;
	float smoothness;
	int numChildren;
};

float CalcSignedDistToShpere(float3 p, float3 center, float radius)
{
	return length(p - center) - radius;
}

float CalcSignedDistToCube(float3 p, float3 center, float3 size)
{
	float3 o = abs(p - center) - size;
	float ud = length(max(o, 0));
	float n = max(max(min(o.x, 0), min(o.y, 0)), min(o.z, 0));
	return ud + n;
}

float CalcSignedDistToTorus(float3 p, float3 center, float r1, float r2)
{
	float2 q = float2(length((p - center).xz) - r1, p.y - center.y);
	return length(q) - r2;
}

float CalcSignedDistToPrism(float3 p, float3 center, float2 h)
{
	p -= center;
	float3 q = abs(p);
	return max(q.z - h.y, max(q.x * 0.866025 + p.y * 0.5, -p.y) - h.x * 0.5);
}

float CalcSignedDistToCylinder(float3 p, float3 center, float2 h)
{
	float3 q = p - center;
	float2 d = abs(float2(length(q.xz), q.y)) - h;
	return length(max(d, 0.0)) + max(min(d.x, 0), min(d.y, 0));
}

float3x3 RotationMatrix(float3 rotation)
{
	float cx = cos(rotation.x);
	float sx = sin(rotation.x);
	float cy = cos(rotation.y);
	float sy = sin(rotation.y);
	float cz = cos(rotation.z);
	float sz = sin(rotation.z);
	float3x3 rotX = float3x3(
		1, 0, 0,
		0, cx, -sx,
		0, sx, cx
	);
	float3x3 rotY = float3x3(
		cy, 0, sy,
		0, 1, 0,
		-sy, 0, cy
	);
	float3x3 rotZ = float3x3(
		cz, -sz, 0,
		sz, cz, 0,
		0, 0, 1
	);
	float3x3 rot = mul(rotZ, mul(rotY, rotX));
	return rot;
}

float CalcSignedDistance(float3 p, Shape shape)
{
	p = mul(RotationMatrix(-shape.rotation * DEG_TO_RAD), p - shape.position) + shape.position;
	
	switch (shape.shapeType)
	{
		case 0: // sphere
			return CalcSignedDistToShpere(p, shape.position, shape.size.x);
		case 1: // cube
			return CalcSignedDistToCube(p, shape.position, shape.size);
		case 2: // torus
			return CalcSignedDistToTorus(p, shape.position, shape.size.x, shape.size.y);
		case 3: // prism
			return CalcSignedDistToPrism(p, shape.position, float2(shape.size.x, shape.size.z));
		case 4: // cylinder
			return CalcSignedDistToCylinder(p, shape.position, float2(shape.size.x, shape.size.y));
		default: // unknown shape type
			return MAX_DISTANCE;
	}
}