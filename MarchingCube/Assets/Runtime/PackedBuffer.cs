using System;
using UnityEngine;
using UnityEngine.UIElements;

public class PackedBuffer
{
	private uint[] buffer;
	private int elemSize;
	private int bitPerElement;
	public PackedBuffer(int size, int bitPerElement)
	{
		this.elemSize = Mathf.CeilToInt(size / bitPerElement);
		this.bitPerElement = bitPerElement;
		buffer = new uint[elemSize];
	}

	public uint[] RawData => buffer;

	public uint GetElement(int idx)
	{
		int packedIdx = idx / bitPerElement;
		int offset = idx % bitPerElement;
		return (buffer[packedIdx] << (offset * bitPerElement)) & 0xF;
	}

	public void SetElement(uint value, int idx)
	{
		int packedIdx = idx / bitPerElement;
		int offset = idx % bitPerElement;
		int mask = 0xF << (offset * bitPerElement);
		buffer[packedIdx] = (uint)(buffer[packedIdx] & ~mask) | ((value & 0xF) << (offset * bitPerElement));
	}

	public uint this[int index]
	{
		get
		{
			return GetElement(index);
		}

		set
		{
			SetElement(value, index);
		}
	}
}

