using System.Collections.Generic;
using UnityEngine;

namespace Stunts
{
	public enum CollisionShape
	{
		PhysicsMesh,
		RenderedMesh
	}

	public enum CarModel
	{
		InGame,
		ShowRoom
	}

	public class ImportSettings : ScriptableObject
	{
		public CollisionShape terrainCollisionShape = CollisionShape.RenderedMesh;
		public CollisionShape carCollisionShape = CollisionShape.RenderedMesh;
		public CarModel carModel = CarModel.ShowRoom;
		public bool fixShowRoomModelBottoms = true;

		public Material materialWithoutBias;
		public Material materialWithBias;
		public Material materialForTerrain;

		public List<Color> palette;
		public List<MaterialDescription> materialDescriptions;

		public List<TileDescription> tileDescriptions;

		public bool AreMaterialsInitialised
		{
			get {
				return
					materialWithoutBias != null && materialWithBias != null &&
					palette != null && palette.Count >= 0x100 &&
					materialDescriptions != null && materialDescriptions.Count >= 0x81;
			}
		}

		public void SetDefaultMaterials()
		{
			materialWithoutBias = Resources.Load<Material>("Materials/StuntsSurface-ZBiasOff");
			materialWithBias = Resources.Load<Material>("Materials/StuntsSurface-ZBiasOn");
			materialForTerrain = Resources.Load<Material>("Materials/StuntsSurface-ZBiasTerrain");
		}
	}
}