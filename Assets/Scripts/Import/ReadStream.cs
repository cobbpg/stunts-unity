using System;
using System.Text;
using UnityEngine;

namespace Stunts
{
	public class ReadStream
	{
		private readonly byte[] data;
		private int position;

		public ReadStream(byte[] data, int startOffset = 0)
		{
			this.data = data;
			position = startOffset;
		}

		public void Skip(int length)
		{
			position += length;
		}

		public byte ReadByte()
		{
			byte result = data[position];
			position++;
			return result;
		}

		public short ReadShort()
		{
			short result = (short)(data[position] + (data[position + 1] << 8));
			position += 2;
			return result;
		}

		public int ReadInt()
		{
			int result = data[position] + (data[position + 1] << 8) + (data[position + 2] << 16) + (data[position + 3] << 24);
			position += 4;
			return result;
		}

		public Vector3 ReadVertex()
		{
			int x = ReadShort();
			int y = ReadShort();
			int z = ReadShort();
			return new Vector3(x, y, -z);
		}

		public byte[] ReadByteArray(int length)
		{
			var result = new byte[length];
			Array.Copy(data, position, result, 0, length);
			position += length;
			return result;
		}

		public string ReadFixedLengthString(int length)
		{
			return ReadStringInternal(length);
		}

		public string ReadString()
		{
			return ReadStringInternal();
		}

		private string ReadStringInternal(int maxLength = -1)
		{
			var builder = new StringBuilder();
			int remaining = maxLength;
			do
			{
				if (remaining == 0)
				{
					break;
				}
				int next = ReadByte();
				remaining--;
				if (next == 0)
				{
					break;
				}
				builder.Append(Char.ConvertFromUtf32(next));
			}
			while (true);
			if (remaining > 0)
			{
				position += remaining;
			}
			return builder.ToString();
		}
	}
}
