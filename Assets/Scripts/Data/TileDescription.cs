using System;

namespace Stunts
{
	[Serializable]
	public class TileDescription
	{
		public static readonly string[] OrientationNames = new[] { "N", "E", "S", "W" };

		public string name;
		public string meshName;
		public int materialIndex;
		public int xSize;
		public int ySize;
		public int orientation;
		public int otherPartIndex;

		public string Displayname
		{
			get {
				return string.IsNullOrEmpty(meshName) ? "UNKNOWN" : meshName + " " + OrientationNames[orientation];
			}
		}
	}
}

