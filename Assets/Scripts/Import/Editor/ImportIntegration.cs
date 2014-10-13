using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
	
namespace Stunts
{
	public class ProgressStatus
	{
		public string description;
		public float progress;
		
		public ProgressStatus(string description, float progress)
		{
			this.description = description;
			this.progress = progress;
		}
	}
	
	public class ImportIntegration : MonoBehaviour
	{
		private const string ImportSettingsPath = "Assets/ImportSettings.asset";
		private static byte[] unpackedCode;

		public static ImportSettings GetImportSettings()
		{
			return FindOrCreateAssetAtPath<ImportSettings>(ImportSettingsPath);
		}

		public static void ClearImportedAssets()
		{
			ClearDirectory("Assets/Resources/Cars", "InGameMeshes");
			ClearDirectory("Assets/Resources/Cars", "ShowRoomMeshes");
			ClearDirectory("Assets/Resources/Cars", "CollisionMeshes");
			ClearDirectory("Assets/Resources", "Terrain");
		}

		private static void ClearDirectory(string parent, string name)
		{
			AssetDatabase.DeleteAsset(parent + "/" + name);
			AssetDatabase.CreateFolder(parent, name);
		}

		private static void UnpackCode()
		{
			if (unpackedCode != null)
			{
				return;
			}

			var executableImage = ExeLoaderEmulation.CreateExePackedImage();
			unpackedCode = ExeLoaderEmulation.ExtractDataFromPackedExecutable(executableImage);
		}

		public static void GenerateTileMapping()
		{
			const int baseAddress = 0xabcc;
			const int nameBaseAddress = 0x9584;
			const int nameSize = 5;
			const int shapeBaseAddress = 0x764c;
			const int shapeSize = 22;

			var importSettings = FindOrCreateAssetAtPath<ImportSettings>(ImportSettingsPath);
			UnpackCode();

			importSettings.tileDescriptions = new List<TileDescription>();
			for (int i = 0; i < 0xf7; i++)
			{
				var tileDescription = new TileDescription();
				int structAddress = baseAddress + i * 14;

				tileDescription.orientation = unpackedCode[structAddress + 3] & 0x03;
				tileDescription.otherPartIndex = unpackedCode[structAddress + 8];
				tileDescription.materialIndex = unpackedCode[structAddress + 9];

				int sizeFlags = unpackedCode[structAddress + 11];
				tileDescription.xSize = (sizeFlags & 2) == 0 ? 1 : 2;
				tileDescription.ySize = (sizeFlags & 1) == 0 ? 1 : 2;

				int shapeAddress = unpackedCode[structAddress + 4] + unpackedCode[structAddress + 5] * 0x100;
				int nameOffset = (shapeAddress - shapeBaseAddress) * nameSize / shapeSize + nameBaseAddress;
				string meshName = new ReadStream(unpackedCode, nameOffset).ReadFixedLengthString(4);
				if (!meshName.All(c => c > 0x20 && c < 0x80))
				{
					meshName = "";
				}

				tileDescription.meshName = meshName;
				tileDescription.name = string.Format("{0} ({1})", i, tileDescription.Displayname);

				importSettings.tileDescriptions.Add(tileDescription);
			}
		}

		public static void GenerateMaterialMapping()
		{
			const int paletteOffset = 0x10;
			const int materialCount = 129;
			const int materialIndexOffset = 0xdbc8;
			const int materialOpaqueInfoOffset = materialIndexOffset + materialCount * 2;
			const int materialMaskOffset = materialIndexOffset + materialCount * 4;
			const ushort opaqueMask = 0xffff;

			var importSettings = FindOrCreateAssetAtPath<ImportSettings>(ImportSettingsPath);
			importSettings.SetDefaultMaterials();
			UnpackCode();

			var paletteData = Resource.ReadResourceMap(Resource.Load("SDMAIN.PVS"))["!pal"];
			importSettings.palette = new List<Color>();
			for (int i = paletteOffset; i < paletteData.Length; i += 3)
			{
				var color = new Color(paletteData[i] / 63f, paletteData[i + 1] / 63f, paletteData[i + 2] / 63f);
				importSettings.palette.Add(color);
			}

			importSettings.materialDescriptions = new List<MaterialDescription>();
			for (int i = 0; i < materialCount; i++)
			{
				var paletteIndex = unpackedCode[materialIndexOffset + i * 2];
				var isOpaque = unpackedCode[materialOpaqueInfoOffset + i * 2] == 0;
				var mask = isOpaque ? opaqueMask : ExeLoaderEmulation.ReadUshortAt(unpackedCode, materialMaskOffset + i * 2);
				var materialDescription = new MaterialDescription(paletteIndex, isOpaque, mask);
				importSettings.materialDescriptions.Add(materialDescription);
			}
		}

		public static IEnumerable<ProgressStatus> ImportMeshes()
		{
			var importSettings = FindOrCreateAssetAtPath<ImportSettings>(ImportSettingsPath);
			var carCodes = GetCarCodes();
			float progress = 0;
			float progressStep = 1f / carCodes.Count;

			Directory.CreateDirectory("Assets/Resources/Cars/InGameMeshes");
			Directory.CreateDirectory("Assets/Resources/Cars/ShowRoomMeshes");
			Directory.CreateDirectory("Assets/Resources/Cars/CollisionMeshes");
			Directory.CreateDirectory("Assets/Resources/Terrain");

			foreach (var carCode in carCodes)
			{
				var dataResource = Resource.ReadResourceMap(Resource.Load("CAR" + carCode + ".RES"));
				var carName = new ReadStream(dataResource["gnam"]).ReadString().Replace('/', '-');
				yield return new ProgressStatus("Importing cars (" + carName + ")", progress);
				progress += progressStep;
				var modelResource = Resource.ReadResourceMap(Resource.Load("ST" + carCode + ".P3S"));
				var inGameModel = new Model(new ReadStream(modelResource["car1"])); 
				var showRoomModel = new Model(new ReadStream(modelResource["car0"])); 
				var collisionModel = new Model(new ReadStream(modelResource["car2"]));
				var inGameMesh = inGameModel.ToMesh(importSettings);
				var showRoomMesh = showRoomModel.ToMesh(importSettings);
				var collisionMesh = collisionModel.ToMesh(importSettings);
				AssetDatabase.CreateAsset(inGameMesh, "Assets/Resources/Cars/InGameMeshes/" + carName + ".asset");
				AssetDatabase.CreateAsset(showRoomMesh, "Assets/Resources/Cars/ShowRoomMeshes/" + carName + ".asset");
				AssetDatabase.CreateAsset(collisionMesh, "Assets/Resources/Cars/CollisionMeshes/" + carName + ".asset");
			}

			var resourceMap1 = Resource.ReadResourceMap(Resource.Load("GAME1.P3S"));
			var resourceMap2 = Resource.ReadResourceMap(Resource.Load("GAME2.P3S"));
			progress = 0;
			progressStep = 1f / (resourceMap1.Count + resourceMap2.Count);
			foreach (var resource in resourceMap1.Concat(resourceMap2))
			{
				yield return new ProgressStatus("Importing tiles (" + resource.Key + ")", progress);
				progress += progressStep;
				var mesh = new Model(new ReadStream(resource.Value)).ToMesh(importSettings);
				AssetDatabase.CreateAsset(mesh, "Assets/Resources/Terrain/" + resource.Key + ".asset");
			}
			
			yield return null;
		}

		private static readonly Dictionary<int, byte> slopeMapping = new Dictionary<int, byte>
		{
			{ 0x0704, 0xb6 }, { 0x0805, 0xb7 }, { 0x0904, 0xb8 }, { 0x0a05, 0xb9 },
			{ 0x070e, 0xba }, { 0x080f, 0xbb }, { 0x090e, 0xbc }, { 0x0a0f, 0xbd },
			{ 0x0718, 0xbe }, { 0x0819, 0xbf }, { 0x0918, 0xc0 }, { 0x0a19, 0xc1 },
			{ 0x0727, 0xc2 }, { 0x0824, 0xc3 }, { 0x0926, 0xc4 }, { 0x0a25, 0xc5 },
			{ 0x073b, 0xc2 }, { 0x0838, 0xc3 }, { 0x093a, 0xc4 }, { 0x0a39, 0xc5 }
		};

		public static IEnumerable<ProgressStatus> LoadTrack(string path)
		{
			const byte hillTopCode = 0x06;

			var data = File.ReadAllBytes(path);
			var importSettings = FindOrCreateAssetAtPath<ImportSettings>(ImportSettingsPath);
			var root = new GameObject(Path.GetFileName(path));

			for (int i = 0; i < 30; i++)
			{
				yield return new ProgressStatus("Building Terrain", i / 30f);
				for (int j = 0; j < 30; j++)
				{
					var index = data[901 + i * 30 + j];
					if (index > 0)
					{
						PlaceTile(importSettings, root.transform, index + 215, j, i, index == hillTopCode);
					}
				}
			}

			for (int i = 0; i < 30; i++)
			{
				yield return new ProgressStatus("Adding Track Elements", i / 30f);
				for (int j = 0; j < 30; j++)
				{
					var index = data[870 - i * 30 + j];
					if (index > 0 && index < 0xf7)
					{
						var finalIndex = index;
						var terrainIndex = data[901 + i * 30 + j];
						if (!slopeMapping.TryGetValue(index + (terrainIndex << 8), out finalIndex))
						{
							finalIndex = index;
						}
						PlaceTile(importSettings, root.transform, finalIndex, j, i, terrainIndex == hillTopCode);
					}
				}
			}

			root.transform.localScale = Vector3.one / 128f;
			yield return null;
		}

		private static void PlaceTile(ImportSettings settings, Transform root, int index, int x, int y, bool hillTop)
		{
			var description = settings.tileDescriptions[index];
			var meshName = description.meshName;
			if (string.IsNullOrEmpty(meshName))
			{
				return;
			}
			var mesh = Resources.Load<Mesh>("Terrain/" + meshName);
			var gameObject = new GameObject(x.ToString("d02") + "-" + y.ToString("d02") + "-" + meshName);
			gameObject.transform.parent = root;
			gameObject.transform.localPosition = new Vector3((2 * x + description.xSize) * 512, hillTop ? 450 : 0, -(2 * y + description.ySize) * 512);
			gameObject.transform.localEulerAngles = new Vector3(0, description.orientation * 90, 0);
			gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
			gameObject.AddComponent<MeshRenderer>().sharedMaterials = index >= 221 && index <= 233
				? new Material[] { settings.materialForTerrain }
				: new Material[] { settings.materialWithoutBias, settings.materialWithBias };
			if (description.otherPartIndex > 0)
			{
				PlaceTile(settings, root, description.otherPartIndex, x, y, hillTop);
			}
		}

		public static T FindOrCreateAssetAtPath<T>(string path) where T : ScriptableObject
		{
			var asset = AssetDatabase.LoadMainAssetAtPath(path) as T;
			if (asset == null)
			{
				asset = ScriptableObject.CreateInstance<T>();
				AssetDatabase.CreateAsset(asset, path);
				AssetDatabase.SaveAssets();
			}
			return asset;
		}

		public static List<string> GetCarCodes()
		{
			var carCodes = new List<string>();
			var gameFiles = Directory.GetFiles(Resource.GameDataPath);
			var carFileRegex = new Regex(@"CAR(.+).RES", RegexOptions.IgnoreCase);
			foreach (var gameFile in gameFiles)
			{
				var match = carFileRegex.Match(gameFile);
				if (match.Success)
				{
					carCodes.Add(match.Groups[1].Value.ToUpper());
				}
			}
			return carCodes;
		}
	}
}
