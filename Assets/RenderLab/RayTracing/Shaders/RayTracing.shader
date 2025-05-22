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

			float3 viewParams;
			float4x4 cameraLocalToWorldMatrix;

			StructuredBuffer<Sphere> spheres;
			int numSpheres;

			int maxRayBounceCount;
			int numRaysPerPixel;
			int frame;

			float4 groundColor;
			float4 skyColorHorizon;
			float4 skyColorZenith;
			float sunFocus;
			float sunIntensity;


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
						hitInfo.material = sphere.material;
					}
				}

				return hitInfo;
			}

			float3 GetEnvironmentLight(Ray ray)
			{
				float skyGradientT = pow(smoothstep(0, 0.4, ray.dir.y), 0.35);
				float groundToSkyT = smoothstep(-0.01, 0, ray.dir.y);
				float3 skyGradient = lerp(skyColorHorizon, skyColorZenith, skyGradientT);
				float sun = pow(max(0, dot(ray.dir, _WorldSpaceLightPos0.xyz)), sunFocus) * sunIntensity;
				// Combine ground, sky, and sun
				float3 composite = lerp(groundColor, skyGradient, groundToSkyT) + sun * (groundToSkyT>=1);
				return composite;
			}

			HitInfo CalculateRayCollision(Ray ray)
			{
				HitInfo closestHit = (HitInfo)0;
				closestHit.distance = 9999.9;

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
				
				return closestHit;
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
						ray.origin = hitInfo.hitPoint;
						ray.dir = RandHemisphereDirection(hitInfo.normal, state);

						RayTracingMaterial material = hitInfo.material;
						float3 emittedLight = material.emissionColor * material.emissionStrength;
						incomingLight += emittedLight * rayColor;
						rayColor *= material.color;
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

				float3 viewPointLocal = float3(i.uv - 0.5, 1.0) * viewParams;
				float3 viewPoint = mul(cameraLocalToWorldMatrix, float4(viewPointLocal, 1.0));

				Ray ray;
				ray.origin = _WorldSpaceCameraPos;
				ray.dir = normalize(viewPoint - ray.origin);

				float3 totalIncomingLight = 0;
				for (int i = 0; i < numRaysPerPixel; ++i)
				{
					totalIncomingLight += Trace(ray, randState);
				}

				float3 pixelColor = totalIncomingLight / numRaysPerPixel;
				return float4(pixelColor, 1.0);
			}

		ENDCG
		}
	}
}
