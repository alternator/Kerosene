using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;

namespace ICKX.Kerosene {
	public class AssetBundlesMenuItems {
		public const string kSimulationMode = "ICKX/AssetBundles/Simulation Mode";
		public const string kUseLocalBuildMode = "ICKX/AssetBundles/UseLoaclBuild Mode";
		public const string kShowToggleProjectView = "ICKX/AssetBundles/Show Toggle ProjectView";

		[MenuItem (kSimulationMode)]
		public static void ToggleSimulationMode() {
			AssetBundleManager.simulateAssetBundleInEditor = !AssetBundleManager.simulateAssetBundleInEditor;
		}

		[MenuItem(kSimulationMode, true)]
		public static bool ToggleSimulationModeValidate() {
			Menu.SetChecked(kSimulationMode, AssetBundleManager.simulateAssetBundleInEditor);
			return true;
		}

		[MenuItem (kUseLocalBuildMode)]
		public static void ToggleUseLocalBuildMode () {
			AssetBundleManager.useLocalBuildAssetBundleInEditor = !AssetBundleManager.useLocalBuildAssetBundleInEditor;
		}

		[MenuItem (kUseLocalBuildMode, true)]
		public static bool ToggleUseLocalBuildModeValidate () {
			Menu.SetChecked (kUseLocalBuildMode, AssetBundleManager.useLocalBuildAssetBundleInEditor);
			return true;
		}

		[MenuItem (kShowToggleProjectView)]
		public static void ToggleShowToggleProjectView () {
			AssetBundleManager.showToggleProjectViewInEditor = !AssetBundleManager.showToggleProjectViewInEditor;
		}

		[MenuItem (kShowToggleProjectView, true)]
		public static bool ToggleShowToggleProjectViewValidate () {
			Menu.SetChecked (kShowToggleProjectView, AssetBundleManager.showToggleProjectViewInEditor);
			return true;
		}

		[MenuItem ("ICKX/AssetBundles/ResetAllAssetBundle")]
		static public void ResetAllAssetBundle () {
			foreach (string path in AssetDatabase.GetAllAssetPaths()) {
				var assetImporter = AssetImporter.GetAtPath (path);

				if (assetImporter == null) return;

				if (!string.IsNullOrEmpty (assetImporter.assetBundleName)) {
					Debug.Log (assetImporter.assetBundleName);
					assetImporter.assetBundleName = null;
					assetImporter.SaveAndReimport ();
				}
			}
		}

		[MenuItem ("ICKX/AssetBundles/Build AssetBundles")]
		static public void BuildAssetBundles() {
			// Choose the output path according to the build target.
			string outputPath = Path.Combine(AssetBundleSettings.Data.directortyName
				, AssetBundleUtility.GetPlatformName());
			if (!Directory.Exists(outputPath))
				Directory.CreateDirectory(outputPath);

			//@TODO: use append hash... (Make sure pipeline works correctly with it.)
			BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.IgnoreTypeTreeChanges, EditorUserBuildSettings.activeBuildTarget);
			ContentsCatalogMaker.CreateCatalog ();

			//RenameManifestFile (outputPath);
		}

		static void RenameManifestFile (string outputPath) {
			string manifestPath = outputPath + "/Windows";

			System.IO.File.Delete (outputPath + "/Default");
			System.IO.File.Delete (outputPath + "/Default.manifest");

			System.IO.File.Move (manifestPath, outputPath + "/Default");
			System.IO.File.Move (manifestPath + ".manifest", outputPath + "/Default.manifest");
		}
	}
}