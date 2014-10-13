using System;

namespace Stunts
{
	public enum PrimitiveType
	{
		Particle,
		Line,
		Polygon,
		Sphere,
		Wheel,
		Ignored
	}

	public class Primitive
	{
		public readonly PrimitiveType Type;
		public readonly bool IsTwoSided;
		public readonly bool HasZBias;
		public readonly byte[] Materials;
		public readonly byte[] Indices;

		public Primitive(int paintJobCount, ReadStream input)
		{
			var primitiveCode = input.ReadByte();
			int indexCount = primitiveCode;
			switch (primitiveCode)
			{
			case 1:
				Type = PrimitiveType.Particle;
				break;
			case 2:
				Type = PrimitiveType.Line;
				break;
			case 11:
				Type = PrimitiveType.Sphere;
				indexCount = 2;
				break;
			case 12:
				Type = PrimitiveType.Wheel;
				indexCount = 6;
				break;
			default:
				if (3 <= primitiveCode && primitiveCode <= 10)
				{
					Type = PrimitiveType.Polygon;
				}
				else
				{
					Type = PrimitiveType.Ignored;
					indexCount = 0;
				}
				break;
			}

			var flags = input.ReadByte();
			IsTwoSided = (flags & 1) != 0;
			HasZBias = (flags & 2) != 0;

			Materials = input.ReadByteArray(paintJobCount);
			Indices = input.ReadByteArray(indexCount);
		}
	}
}

