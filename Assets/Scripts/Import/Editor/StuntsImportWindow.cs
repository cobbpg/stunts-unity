using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Stunts
{
	public class StuntsImportWindow : EditorWindow
	{
		private IEnumerator<ProgressStatus> importProcess;
		private string importProcessTitle;

		[MenuItem("Window/Stunts Import Tool")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<StuntsImportWindow>("Stunts Import Tool");
		}
	
		void OnGUI()
		{
			ShowImportProgress();

			var importSettings = ImportIntegration.GetImportSettings();
			if (GUILayout.Button("Edit Import Settings"))
			{
				Selection.activeObject = importSettings;
			}
			if (!File.Exists(Path.Combine(Resource.GameDataPath, "LOAD.EXE")))
			{
				EditorGUILayout.HelpBox(
					"The original Stunts 1.1 must be copied into the " +
					Resource.GameDataPath + " directory!", MessageType.Info
				);
				return;
			}

			if (GUILayout.Button("Import All Defaults"))
			{
				ImportIntegration.GenerateMaterialMapping();
				ImportIntegration.GenerateTileMapping();
			}

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Import Default Materials"))
			{
				ImportIntegration.GenerateMaterialMapping();
			}
			var materialsAvailable = importSettings.AreMaterialsInitialised;
			DrawCheckBox(materialsAvailable);
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Import Default Tile Descriptions"))
			{
				ImportIntegration.GenerateTileMapping();
			}
			var tileMappingAvailable = importSettings.tileDescriptions != null && importSettings.tileDescriptions.Count >= 0xf7;
			DrawCheckBox(tileMappingAvailable);
			GUILayout.EndHorizontal();

			if (materialsAvailable)
			{
				if (GUILayout.Button("Import All Models"))
				{
					importProcess = ImportIntegration.ImportMeshes().GetEnumerator();
					importProcessTitle = "Importing Meshes";
				}
			}
			else
			{
				EditorGUILayout.HelpBox("Material descriptions are required to be able to import models.", MessageType.Warning);
			}

			if (GUILayout.Button("Clear Imported Assets"))
			{
				ImportIntegration.ClearImportedAssets();
			}

			if (materialsAvailable && tileMappingAvailable)
			{
				if (GUILayout.Button("Load Track"))
				{
					var trackPath = EditorUtility.OpenFilePanel("Select Track", "", "trk");
					if (trackPath.Length != 0)
					{
						importProcess = ImportIntegration.LoadTrack(trackPath).GetEnumerator();
						importProcessTitle = "Loading Track " + Path.GetFileName(trackPath);
					}
				}
			}
			else
			{
				EditorGUILayout.HelpBox("Material and tile descriptions are required to be able to load tracks.", MessageType.Warning);
			}
		}

		void ShowImportProgress()
		{
			if (importProcess == null)
			{
				EditorUtility.ClearProgressBar();
				return;
			}

			importProcess.MoveNext();
			var progress = importProcess.Current;
			if (progress == null || EditorUtility.DisplayCancelableProgressBar(importProcessTitle, progress.description, progress.progress))
			{
				EditorUtility.ClearProgressBar();
				importProcess = null;
			}
			Repaint();
		}

		private void DrawCheckBox(bool isChecked)
		{
			GUI.enabled = false;
			GUILayout.Toggle(isChecked, "", GUILayout.Width(20));
			GUI.enabled = true;
		}
	}
}
