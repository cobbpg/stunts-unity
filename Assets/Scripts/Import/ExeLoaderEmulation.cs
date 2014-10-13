using System;

namespace Stunts
{
	public class ExeLoaderEmulation
	{
		public static byte[] CreateExePackedImage()
		{
			var egaCmn = Resource.Load("EGA.CMN");
			var mcgaCod = Resource.Load("MCGA.COD");
			var mcgaDif = Resource.Load("MCGA.DIF");
			var mcgaHdr = Resource.Load("MCGA.HDR");
			
			var bytesInLastPage = ReadUshortAt(mcgaHdr, 2);
			var pagesInExecutable = ReadUshortAt(mcgaHdr, 4);
			var paragraphsInHeader = ReadUshortAt(mcgaHdr, 8);
			
			int executableSize = pagesInExecutable << 9;
			if (bytesInLastPage > 0)
			{
				executableSize += bytesInLastPage - 0x1f0;
			}
			
			int headerSize = paragraphsInHeader << 4;
			
			var executableImage = new byte[executableSize];
			mcgaHdr.CopyTo(executableImage, 0);
			egaCmn.CopyTo(executableImage, headerSize);
			mcgaCod.CopyTo(executableImage, headerSize + egaCmn.Length);

			var difStream = new ReadStream(mcgaDif);
			int writeOffset = headerSize - 1;
			while (true)
			{
				var jump = (ushort)difStream.ReadShort();
				if (jump == 0)
				{
					break;
				}
				writeOffset += jump & 0x7fff;
				executableImage[writeOffset] = difStream.ReadByte();
				executableImage[writeOffset + 1] = difStream.ReadByte();
				if ((jump & 0x8000) != 0)
				{
					executableImage[writeOffset + 2] = difStream.ReadByte();
					executableImage[writeOffset + 3] = difStream.ReadByte();
				}
			}

			return executableImage;
		}

		public static byte[] ExtractDataFromPackedExecutable(byte[] image)
		{
			var paragraphsInHeader = ReadUshortAt(image, 8);
			var codeSegment = ReadUshortAt(image, 22);
			var exepackOffset = (paragraphsInHeader + codeSegment) << 4;
			var destinationLength = ReadUshortAt(image, exepackOffset + 12);
			var skipLength = ReadUshortAt(image, exepackOffset + 14);
			var packedDataStart = paragraphsInHeader << 4;
			var packedDataLength = exepackOffset - ((skipLength - 1) << 4) - packedDataStart;
			var unpackedData = new byte[destinationLength << 4];
			
			int index = packedDataStart + packedDataLength - 1;
			int writeIndex = unpackedData.Length - 1;
			while (image[index] == 0xff)
			{
				index--;
			}
			while (true)
			{
				var opcode = image[index];
				var count = image[index - 1] * 0x100 + image[index - 2];
				switch (opcode & 0xfe)
				{
					case 0xb0:
						var fill = image[index - 3];
						for (int i = 0; i < count; i++)
						{
							unpackedData[writeIndex] = fill;
							writeIndex--;
						}
						index -= 4;
						break;
					case 0xb2:
						Array.Copy(image, index - 2 - count, unpackedData, writeIndex - count + 1, count);
						index -= 3 + count;
						writeIndex -= count;
						break;
				}
				if ((opcode & 1) != 0)
				{
					break;
				}
			}
			
			var result = new byte[unpackedData.Length - 1 - writeIndex];
			Array.Copy(unpackedData, writeIndex + 1, result, 0, result.Length);
			
			return result;
		}

		public static ushort ReadUshortAt(byte[] buffer, int offset)
		{
			return (ushort)(buffer[offset] + (buffer[offset + 1] << 8));
		}
	}
}

