Shader "Custom/RayTracing"
{
	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		
		Pass
		{
			CGPROGRAM
			#pragma vertex vert;
			#pragma fragment frag;
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			struct Ray
			{
				float3 origin;
				float3 dir;
			};

			struct RayTracingMaterial
			{
				float4 color;
				float4 emissionColor;
				float4 specularColor;
				float emissionStrength;
				float smoothness;
				float specularProbability;
				int flag;
			};

			struct HitInfo
			{
				bool bHit;
				float distance;
				float3 hitPoint;
				float3 normal;
				RayTracingMaterial material;
			};

			struct Sphere
			{
				float3 position;
				float radius;
				RayTracingMaterial material;
			};

			struct Triangle
			{
				float3 posA, posB, posC;
				float3 normalA, normalB, normalC;
			};

			struct MeshInfo
			{
				uint firstTriangleIndex;
				uint numTriangles;
				RayTracingMaterial material;
				float3 boundsMin;
				float3 boundsMax;
			};

			float3 viewParams;
			float4x4 cameraLocalToWorldMatrix;

			StructuredBuffer<Sphere> spheres;
			int numSpheres;

			StructuredBuffer<Triangle> triangles;
			StructuredBuffer<MeshInfo> meshInfos;
			int numMeshes;

			int maxRayBounceCount;
			int numRaysPerPixel;
			int frame;

			int useEnvironmentLighting;
			float4 groundColor;
			float4 skyColorHorizon;
			float4 skyColorZenith;
			float sunFocus;
			float sunIntensity;

			float divergeStrength;
			float defocusStrength;


			float3 GetEnvironmentLight(Ray ray)
			{
				if (!useEnvironmentLighting)
					return 0;

				float skyGradientT = pow(smoothstep(0, 0.4, ray.dir.y), 0.35);
				float groundToSkyT = smoothstep(-0.01, 0, ray.dir.y);
				float3 skyGradient = lerp(skyColorHorizon, skyColorZenith, skyGradientT);
				float sun = pow(max(0, dot(ray.dir, _WorldSpaceLightPos0.xyz)), sunFocus) * sunIntensity;
				// Combine ground, sky, and sun
				float3 composite = lerp(groundColor, skyGradient, groundToSkyT) + sun * (groundToSkyT>=1);
				return composite;
			}

			// PCG (permuted congruential generator)
			// https://en.wikipedia.org/wiki/Permuted_congruential_generator
			float Rand(inout uint state)
			{
				state = state * 74779605 + 2891336453;
				uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
				result = (result >> 22) ^ result;
				return result / 4294967295.0; // 2^32 - 1
			}

			float RandNormalDistribution(inout uint state)
			{
				float theta = 2.0 * 3.14159265358979323846 * Rand(state);
				float rho = sqrt(-2.0 * log(Rand(state)));
				return rho * cos(theta);
			}

			float3 RandDirection(inout uint state)
			{
				float x = RandNormalDistribution(state);
				float y = RandNormalDistribution(state);
				float z = RandNormalDistribution(state);
				return normalize(float3(x,y,z));
			}
			
			float3 RandHemisphereDirection(float3 normal, inout uint state)
			{
				float3 dir = RandDirection(state);
				return dir * sign(dot(normal, dir));
			}

			float2 RandPointInCircle(inout uint state)
			{
				float theta = 2.0 * 3.14159265358979323846 * Rand(state);
				float r = sqrt(Rand(state));
				return float2(cos(theta), sin(theta)) * r;
			}

			bool RayBoundingBox(Ray ray, float3 boxMin, float3 boxMax)
			{
				float3 invDir = 1 / ray.dir;
				float3 tMin = (boxMin - ray.origin) * invDir;
				float3 tMax = (boxMax - ray.origin) * invDir;
				float3 t1 = min(tMin, tMax);
				float3 t2 = max(tMin, tMax);
				float tNear = max(max(t1.x, t1.y), t1.z);
				float tFar = min(min(t2.x, t2.y), t2.z);
				return tNear <= tFar;
			};

			HitInfo RaySphere(Ray ray, Sphere sphere)
			{
				HitInfo hitInfo = (HitInfo)0;
				float3 offsetToRayOrigin = ray.origin - sphere.position;

				float a = dot(ray.dir, ray.dir);
				float b = 2.0 * dot(offsetToRayOrigin, ray.dir);
				float c = dot(offsetToRayOrigin, offsetToRayOrigin) - sphere.radius * sphere.radius;
				float discriminant = b * b - 4 * a * c;

				if (discriminant >= 0.0)
				{
					float dist = (-b - sqrt(discriminant)) / (2.0 * a);

					if (dist >= 0.0)
					{
						hitInfo.bHit = true;
						hitInfo.distance = dist;
						hitInfo.hitPoint = ray.origin + ray.dir * dist;
						hitInfo.normal = normalize(hitInfo.hitPoint - sphere.position);
					}
				}

				return hitInfo;
			}

			HitInfo RayTriangle(Ray ray, Triangle tri)
			{
				float3 edgeAB = tri.posB - tri.posA;
				float3 edgeAC = tri.posC - tri.posA;
				float3 normalVector = cross(edgeAB, edgeAC);
				float3 ao = ray.origin - tri.posA;
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
				hitInfo.bHit = determinant >= 1E-6 && dist >= 0 && u >= 0 && v >= 0 && w >= 0;
				hitInfo.hitPoint = ray.origin + ray.dir * dist;
				hitInfo.normal = normalize(tri.normalA * w + tri.normalB * u + tri.normalC * v);
				hitInfo.distance = dist;

				return hitInfo;
			}

			HitInfo CalculateRayCollision(Ray ray)
			{
				HitInfo closestHit = (HitInfo)0;
				closestHit.distance = 9999.9;

				// spheres
				for(int i = 0; i < numSpheres; ++i)
				{
					Sphere sphere = spheres[i];
					HitInfo hitInfo = RaySphere(ray, sphere);

					if (hitInfo.bHit && hitInfo.distance < closestHit.distance)
					{
						closestHit = hitInfo;
						closestHit.material = sphere.material;
					}
				}
				// meshes
				for(int m = 0; m < numMeshes; ++m)
				{
					MeshInfo meshInfo = meshInfos[m];
					if (!RayBoundingBox(ray, meshInfo.boundsMin, meshInfo.boundsMax))
						continue;

					for(int t = 0; t < meshInfo.numTriangles; ++t)
					{
						Triangle tri = triangles[meshInfo.firstTriangleIndex + t];
						HitInfo hitInfo = RayTriangle(ray, tri);
						if (hitInfo.bHit && hitInfo.distance < closestHit.distance)
						{
							closestHit = hitInfo;
							closestHit.material = meshInfo.material;
						}
					}
				}
				
				return closestHit;
			}

			float2 mod2(float2 x, float2 y)
			{
				return x - y * floor(x/y);
			}

			float3 Trace(Ray ray, inout uint state)
			{
				float3 incomingLight = 0;
				float3 rayColor = 1;

				for (int i = 0; i <= maxRayBounceCount; ++i)
				{
					HitInfo hitInfo = CalculateRayCollision(ray);

					if (hitInfo.bHit)
					{
						RayTracingMaterial material = hitInfo.material;

						if (material.flag == 1) // checker pattern
						{
							float2 c = mod2(floor(hitInfo.hitPoint.xz), 2.0);
							material.color = c.x == c.y ? material.color : material.emissionColor;
							material.specularColor = material.color;
						}

						bool bSpecularBounce = material.specularProbability >= Rand(state);

						ray.origin = hitInfo.hitPoint;
						float3 diffuseDir = normalize(hitInfo.normal + RandDirection(state));
						float3 specularDir = reflect(ray.dir, hitInfo.normal);
						ray.dir = normalize(lerp(diffuseDir, specularDir, material.smoothness * bSpecularBounce));

						float3 emittedLight = material.emissionColor * material.emissionStrength;
						incomingLight += emittedLight * rayColor;
						rayColor *= lerp(material.color, material.specularColor, bSpecularBounce);

						float p = max(rayColor.r, max(rayColor.g, rayColor.b));
						if (Rand(state) >= p)
							break;
						rayColor *= 1.0f / p; 
					}
					else
					{
						incomingLight += GetEnvironmentLight(ray) * rayColor;
						break;
					}
				}

				return incomingLight;
			}

			float4 frag(v2f i) : SV_Target
			{
				uint2 numPixels = _ScreenParams.xy;
				uint2 pixelCoord = i.uv * numPixels;
				uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
				uint randState = pixelIndex + frame * 719393;

				float3 focusPointLocal = float3(i.uv - 0.5, 1.0) * viewParams;
				float3 focusPoint = mul(cameraLocalToWorldMatrix, float4(focusPointLocal, 1.0));

				float3 cameraRight = cameraLocalToWorldMatrix._m00_m10_m20;
				float3 cameraUp = cameraLocalToWorldMatrix._m01_m11_m21;

				Ray ray;
				float3 totalIncomingLight = 0;
				for (int i = 0; i < numRaysPerPixel; ++i)
				{
					float2 defocusJitter = RandPointInCircle(randState) * defocusStrength / numPixels.x;
					ray.origin = _WorldSpaceCameraPos + defocusJitter.x * cameraRight + defocusJitter.y * cameraUp;

					float2 jitter = RandPointInCircle(randState) * divergeStrength / numPixels.x;
					float3 jitteredFocusPoint = focusPoint + jitter.x * cameraRight + jitter.y * cameraUp;
					ray.dir = normalize(jitteredFocusPoint - ray.origin);

					totalIncomingLight += Trace(ray, randState);
				}

				float3 pixelColor = totalIncomingLight / numRaysPerPixel;
				return float4(pixelColor, 1.0);
			}

		ENDCG
		}
	}
}
