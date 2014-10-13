using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Stunts
{
	public enum ResourceType
	{
		Blob,
		Bitmap,
		Icon,
		Shape,
		Unknown
	}

	public struct ResourceFormat
	{
		public static readonly ResourceFormat Unknown = new ResourceFormat(ResourceType.Unknown, false);
		public readonly ResourceType Type;
		public readonly bool Compressed;
		private static readonly Dictionary<string, ResourceFormat> extensionMap = new Dictionary<string, ResourceFormat>
		{
			{ "RES", new ResourceFormat(ResourceType.Blob, false) },
			{ "PRE", new ResourceFormat(ResourceType.Blob, true) },
			{ "VSH", new ResourceFormat(ResourceType.Bitmap, false) },
			{ "PVS", new ResourceFormat(ResourceType.Bitmap, true) },
			{ "ESH", new ResourceFormat(ResourceType.Icon, false) },
			{ "PES", new ResourceFormat(ResourceType.Icon, true) },
			{ "3SH", new ResourceFormat(ResourceType.Shape, false) },
			{ "P3S", new ResourceFormat(ResourceType.Shape, true) },
			{ "COD", new ResourceFormat(ResourceType.Blob, true) },
			{ "CMN", new ResourceFormat(ResourceType.Blob, true) },
			{ "DIF", new ResourceFormat(ResourceType.Blob, true) },
			{ "HDR", new ResourceFormat(ResourceType.Blob, false) },
		};

		private ResourceFormat(ResourceType type, bool compressed)
		{
			Type = type;
			Compressed = compressed;
		}

		public static ResourceFormat FromExtension(string extension)
		{
			ResourceFormat result;
			if (extensionMap.TryGetValue(extension.ToUpper().TrimStart('.'), out result))
			{
				return result;
			}
			else
			{
				return ResourceFormat.Unknown;
			}
		}
	}

	public class Resource
	{
		public const string GameDataPath = "GameData";

		public static byte[] Load(string fileName)
		{
			var bytes = File.ReadAllBytes(Path.Combine(GameDataPath, fileName));
			if (ResourceFormat.FromExtension(Path.GetExtension(fileName)).Compressed)
			{
				bytes = Unpack(bytes);
			}
			return bytes;
		}

		public static Dictionary<string, byte[]> ReadResourceMap(byte[] rawResource)
		{
			var result = new Dictionary<string, byte[]>();
			var input = new ReadStream(rawResource);
			input.Skip(4);
			var resourceCount = input.ReadShort();

			var ids = new string[resourceCount];
			for (int i = 0; i < resourceCount; i++)
			{
				ids[i] = input.ReadFixedLengthString(4);
			}

			var sortedIds = new SortedDictionary<int, string>();
			for (int i = 0; i < resourceCount; i++)
			{
				sortedIds[input.ReadInt()] = ids[i];
			}

			var data = new ReadBuffer(rawResource).Drop(6 + 8 * resourceCount);
			var sortedOffsets = new List<int>(sortedIds.Keys);
			sortedOffsets.Add(data.Length);

			for (int i = 0; i < resourceCount; i++)
			{
				var startOffset = sortedOffsets[i];
				var endOffset = sortedOffsets[i + 1];
				result[sortedIds[startOffset]] = data.Slice(startOffset, endOffset - startOffset).ToArray();
			}

			return result;
		}

		#region Unpacking

		public static byte[] Unpack(byte[] packedData)
		{
			var buffer = new ReadBuffer(packedData);
			bool singlePass = (buffer[0] & 0x80) == 0;
			int passCount = singlePass ? 1 : buffer[0] & 0x7f; 

			ReadBuffer currentPass = singlePass ? buffer : buffer.Drop(4);
			for (int i = 0; i < passCount; i++)
			{
				currentPass = UnpackPass(currentPass);
			}

			return currentPass.ToArray();
		}

		private static ReadBuffer UnpackPass(ReadBuffer packedData)
		{
			switch (packedData[0])
			{
				case 1:
					return UnpackRLE(packedData.Drop(8));
				case 2:
					return UnpackVLE(packedData.Drop(4));
				default:
					throw new ArgumentException("Unknown compression type!");
			}
		}

		private static ReadBuffer UnpackRLE(ReadBuffer packedData)
		{
			int escCount = packedData[0] & 0x7f;
			var escCodes = packedData.Slice(1, escCount);
			var rleData = packedData.Drop(escCount + 1);
			var escLookup = new byte[0x100];
			for (int i = 0; i < escCount; i++)
			{
				escLookup[escCodes[i]] = (byte)(i + 1);
			}

			bool compressed = (packedData[0] & 0x80) == 0;
			var encodedData = compressed ? UnpackSequences(rleData, escCodes[1]) : rleData;

			var result = new WriteBuffer();
			while (encodedData.Length > 0)
			{
				var code = escLookup[encodedData[0]];
				switch (code)
				{
					case 0:
						result.Append(encodedData[0]);
						encodedData = encodedData.Drop(1);
						break;
					case 1:
						result.AppendRepeat(encodedData[2], encodedData[1]);
						encodedData = encodedData.Drop(3);
						break;
					case 3:
						result.AppendRepeat(encodedData[3], encodedData[1] + (((int)encodedData[2]) << 8));
						encodedData = encodedData.Drop(4);
						break;
					default:
						result.AppendRepeat(encodedData[1], code - 1);
						encodedData = encodedData.Drop(2);
						break;
				}
			}

			return result.ToReadBuffer();
		}

		private static ReadBuffer UnpackSequences(ReadBuffer packedData, byte seqMarker)
		{
			var remaining = packedData;
			var result = new WriteBuffer();
			while (remaining.Length > 0)
			{
				var value = remaining[0];
				remaining = remaining.Drop(1);
				if (value != seqMarker)
				{
					result.Append(value);
				}
				else
				{
					var sequence = remaining.TakeUntil(seqMarker);
					var count = remaining[sequence.Length + 1];
					for (int i = 0; i < count; i++)
					{
						result.Append(sequence);
					}
					remaining = remaining.Drop(sequence.Length + 2);
				}
			}
			return result.ToReadBuffer();
		}

		private static ReadBuffer UnpackVLE(ReadBuffer packedData)
		{
			var widthsCount = packedData[0];
			var widths = packedData.Slice(1, widthsCount);
			var alphabetCount = widths.Select(val => (int)val).Sum();
			var alphabet = packedData.Slice(1 + widthsCount, alphabetCount);

			var dictionary = new HuffmanTree(widths, alphabet);
			var stream = EnumerateBits(packedData.Drop(1 + widthsCount + alphabetCount));

			var result = new WriteBuffer();
			while (true)
			{
				int value = dictionary.DecodeByte(stream);
				if (value < 0)
				{
					break;
				}
				result.Append((byte)value);
			}

			return result.ToReadBuffer();
		}

		private static IEnumerator<bool> EnumerateBits(ReadBuffer data)
		{
			foreach (var value in data)
			{
				int bits = value;
				for (int i = 0; i < 8; i++)
				{
					yield return (bits & 0x80) != 0;
					bits <<= 1;
				}
			}
		}

		private class ReadBuffer : IEnumerable<byte>
		{
			private readonly byte[] data;
			private readonly int start;
			public readonly int Length;

			public ReadBuffer(byte[] data)
			{
				this.data = data;
				start = 0;
				Length = data.Length;
			}

			private ReadBuffer(byte[] data, int start, int length)
			{
				this.data = data;
				this.start = start;
				Length = length;
			}

			public ReadBuffer Slice(int start, int length)
			{
				return new ReadBuffer(data, this.start + start, length);
			}

			public ReadBuffer Take(int length)
			{
				return Slice(0, length);
			}

			public ReadBuffer Drop(int length)
			{
				return Slice(length, Length - length);
			}

			public ReadBuffer TakeUntil(byte value)
			{
				int index = Array.IndexOf(data, value, start);
				if (index < 0)
				{
					index = Length;
				}
				return Take(index - start);
			}

			public byte[] ToArray()
			{
				var result = new byte[Length];
				Array.Copy(data, start, result, 0, Length);
				return result;
			}

			public byte this[int index]
			{
				get { return data[start + index]; }
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public IEnumerator<byte> GetEnumerator()
			{
				int end = start + Length;
				for (int i = start; i < end; i++)
				{
					yield return data[i];
				}
			}
		}

		private class WriteBuffer
		{
			private readonly List<byte> data;

			public WriteBuffer()
			{
				data = new List<byte>();
			}

			public void Append(byte value)
			{
				data.Add(value);
			}

			public void Append(ReadBuffer buffer)
			{
				data.AddRange(buffer);
			}

			public void AppendRepeat(byte value, int count)
			{
				for (int i = 0; i < count; i++)
				{
					data.Add(value);
				}
			}

			public ReadBuffer ToReadBuffer()
			{
				return new ReadBuffer(data.ToArray());
			}
		}

		private class HuffmanTree
		{
			private const int Empty = -1;
			private HuffmanTree left;
			private HuffmanTree right;
			private int value = Empty;

			public HuffmanTree(ReadBuffer widths, ReadBuffer alphabet)
			{
				int level = 1;
				int code = 0;
				int symbolIndex = 0;
				foreach (var width in widths)
				{
					for (int i = 0; i < width; i++)
					{
						AddSymbol(alphabet[symbolIndex], code, level);
						symbolIndex++;
						code++;
					}
					level <<= 1;
					code <<= 1;
				}
			}

			private HuffmanTree()
			{
			}

			public int DecodeByte(IEnumerator<bool> stream)
			{
				var node = this;
				while (node.IsInner())
				{
					if (!stream.MoveNext())
					{
						return -1;
					}
					node = stream.Current ? node.right : node.left;
				}
				return node.value;
			}

			private void AddSymbol(int symbol, int code, int level)
			{
				if (level <= 0)
				{
					value = symbol;
					return;
				}

				if (!IsInner())
				{
					left = new HuffmanTree();
					right = new HuffmanTree();
					left.AddSymbol(symbol, code, level >> 1);
					return;
				}

				if ((code & level) == 0)
				{
					left.AddSymbol(symbol, code, level >> 1);
				}
				else
				{
					right.AddSymbol(symbol, code, level >> 1);
				}
			}

			private bool IsInner()
			{
				return left != null;
			}
		}

		#endregion
	}
}
