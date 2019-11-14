using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace ICKX.Kerosene {

	public class AssetBundleProjectViewToggle {
		
		[InitializeOnLoadMethod]
		static void Initialize () {
			EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
		}

		private static void OnProjectWindowItemGUI (string guid, Rect selectionRect) {
			
			if (!AssetBundleManager.showToggleProjectViewInEditor) return;

			string path = AssetDatabase.GUIDToAssetPath (guid);
			var assetImporter = AssetImporter.GetAtPath (path);

			if (assetImporter == null) return;
			//Debug.Log (path + " : " + assetImporter.assetBundleName);

			bool hasAssetBundleName = !string.IsNullOrEmpty (assetImporter.assetBundleName);

			var toggleRect = selectionRect;
			toggleRect.x += toggleRect.width - 16.0f;
			toggleRect.width = 16.0f;


			if (string.IsNullOrEmpty (assetImporter.assetBundleName)
					&& !string.IsNullOrEmpty (AssetDatabase.GetImplicitAssetBundleName (path))) {
				EditorGUI.showMixedValue = true;
			}

			bool toggle = EditorGUI.Toggle (toggleRect, hasAssetBundleName);

			EditorGUI.showMixedValue = false;

			if (!string.IsNullOrEmpty (assetImporter.assetBundleName)
					|| !string.IsNullOrEmpty (AssetDatabase.GetImplicitAssetBundleName (path))) {
				EditorGUI.DrawRect (selectionRect, new Color (0.0f, 0.0f, 1.0f, 0.1f));
			}

			if (toggle != hasAssetBundleName) {
				if (toggle) {
					if(System.IO.Directory.Exists(path)) {
						assetImporter.assetBundleName = path + "_folder.bundle";
					} else if (System.IO.Path.GetExtension (path) == ".unity") {
						//シーンファイルは1フォルダにまとめる
						assetImporter.assetBundleName = "scene/" + System.IO.Path.GetFileNameWithoutExtension (path) + ".bundle";
					} else if (System.IO.Path.GetExtension (path) == ".shader") {
						//シェーダーは
						assetImporter.assetBundleName = "common_shader.bundle";
					} else {
						assetImporter.assetBundleName = path + ".bundle";
					}
				} else {
					assetImporter.assetBundleName = "";
				}
				EditorUtility.SetDirty (assetImporter);
			}
		}

		//public static void MoveSubEntryToRoot () {
		//	var aaSettings = AddressableAssetSettings.GetDefault (false, false);

		//	// サブエントリを全てグループ直下へ移動
		//	var entries = aaSettings.GetAllAssets (true, true);
		//	foreach (var entry in entries) {
		//		entry.address = entry.assetPath;
		//		aaSettings.CreateOrMoveEntry (entry.guid, entry.parentGroup);
		//	}

		//	// 空になったディレクトリのエントリを削除
		//	entries = aaSettings.GetAllAssets (true, false);
		//	foreach (var entry in entries) {
		//		var isDirectory = System.IO.Directory.Exists (entry.assetPath);
		//		if (isDirectory) aaSettings.RemoveAssetEntry (entry.guid);
		//	}
		//}
	}
}

