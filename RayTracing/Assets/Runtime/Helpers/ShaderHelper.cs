using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RayTracing
{
	public static class ShaderHelper
	{
		public static void InitMaterial(Shader shader, ref Material mat)
		{
			if (mat == null || (mat.shader != shader && shader != null))
			{
				if (shader == null)
				{
					shader = Shader.Find("Unlit/Texture");
				}

				mat = new Material(shader);
			}
		}
		public static RenderTexture CreateRenderTexture(RenderTexture template)
		{
			RenderTexture renderTexture = null;
			CreateRenderTexture(ref renderTexture, template);
			return renderTexture;
		}
		public static RenderTexture CreateRenderTexture(int width, int height, FilterMode filterMode, GraphicsFormat format, string name = "Unnamed", int depth = 0, bool useMipMaps = false)
		{
			RenderTexture texture = new RenderTexture(width, height, depth);
			texture.graphicsFormat = format;
			texture.enableRandomWrite = true;
			texture.autoGenerateMips = false;
			texture.useMipMap = useMipMaps;
			texture.Create();

			texture.name = name;
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = filterMode;
			return texture;
		}
		public static void CreateRenderTexture(ref RenderTexture texture, RenderTexture template)
		{
			if (texture != null)
			{
				texture.Release();
			}
			texture = new RenderTexture(template.descriptor);
			texture.enableRandomWrite = true;
			texture.Create();
		}
		public static bool CreateRenderTexture(ref RenderTexture texture, int width, int height, FilterMode filterMode, GraphicsFormat format, string name = "Unnamed", int depth = 0, bool useMipMaps = false)
		{
			if (texture == null || !texture.IsCreated() || texture.width != width || texture.height != height || texture.graphicsFormat != format || texture.depth != depth || texture.useMipMap != useMipMaps)
			{
				if (texture != null)
				{
					texture.Release();
				}
				texture = CreateRenderTexture(width, height, filterMode, format, name, depth, useMipMaps);
				return true;
			}
			else
			{
				texture.name = name;
				texture.wrapMode = TextureWrapMode.Clamp;
				texture.filterMode = filterMode;
			}

			return false;
		}
		public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count)
		{
			int stride = GetStride<T>();
			bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;
			if (createNewBuffer)
			{
				buffer?.Release();
				buffer = new ComputeBuffer(count, stride);
			}
		}
		public static ComputeBuffer CreateStructuredBuffer<T>(T[] data)
		{
			var buffer = new ComputeBuffer(data.Length, GetStride<T>());
			buffer.SetData(data);
			return buffer;
		}

		public static ComputeBuffer CreateStructuredBuffer<T>(List<T> data) where T : struct
		{
			var buffer = new ComputeBuffer(data.Count, GetStride<T>());
			buffer.SetData(data);

			return buffer;
		}

		public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, List<T> data) where T : struct
		{
			int stride = GetStride<T>();
			bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != data.Count || buffer.stride != stride;
			if (createNewBuffer)
			{
				buffer?.Release();
				buffer = new ComputeBuffer(data.Count, stride);
			}
			buffer.SetData(data);
		}

		public static ComputeBuffer CreateStructuredBuffer<T>(int count)
		{
			return new ComputeBuffer(count, GetStride<T>());
		}
		public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data)
		{
			CreateStructuredBuffer<T>(ref buffer, data.Length);
			buffer.SetData(data);
		}

		public static int GetStride<T>()
		{
			return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
		}

	}
}