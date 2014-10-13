using System;

namespace Stunts
{
	[Serializable]
	public class MaterialDescription
	{
		public int paletteIndex;
		public bool isOpaque;
		public int mask;

		public MaterialDescription(int paletteIndex, bool isOpaque, ushort mask)
		{
			this.paletteIndex = paletteIndex;
			this.isOpaque = isOpaque;
			this.mask = mask;
		}
	}
}

