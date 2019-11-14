using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SerializableCollections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ICKX.Kerosene {

	[System.Serializable]
	public class SerializableContentsCatalog {

		[System.Serializable]
		public struct AssetInfo {
			public string assetId;
			public string bundleName;
			public string assetName;
		}

		public List<AssetInfo> assetInfoTable = new List<AssetInfo>();
	}

	public class ContentsCatalog {

		private static ContentsCatalog s_instance;

		public static ContentsCatalog instance {
			get {
				if(s_instance == null) {
					s_instance = new ContentsCatalog ();
				}
				return s_instance;
			}
		}

		public struct AssetInfo {
			public string packageName;
			public string bundleName;
			public string assetName;
		}

		public List<string> m_packageList = new List<string> ();
		public Dictionary<AssetID, AssetInfo> m_assetInfoTable = new Dictionary<AssetID, AssetInfo>();

		public IReadOnlyList<string> packageList { get { return m_packageList; } }
		public IReadOnlyDictionary<AssetID, AssetInfo> assetInfoTable { get { return m_assetInfoTable; } }

		public static void LoadCatalog () {
			string folderPath = AssetBundleManager.GetAssetBundlePlatformFolderPath ();
			if (!System.IO.Directory.Exists (folderPath)) return;

			var dirInfo = new System.IO.DirectoryInfo (folderPath);

			foreach (var fileInfo in dirInfo.GetFiles ("*.json")) {
				LoadCatalogFilePath (fileInfo.FullName);
			}
		}

		public static void LoadCatalog (string packageName) {
			string folderPath = AssetBundleManager.GetAssetBundlePlatformFolderPath ();
			LoadCatalogFilePath (folderPath + "/" + packageName + "_Catalog.json");
		}

		public static void LoadCatalogFilePath (string filePath) {
			string pkgName = System.IO.Path.GetFileName(filePath).Replace ("_Catalog.json", "");

			if(!instance.m_packageList.Contains(pkgName)) {
				instance.m_packageList.Add (pkgName);
			}

//			filePath = filePath.Replace ('\\', '/');

			string json = System.IO.File.ReadAllText (filePath);
			var catalog = JsonUtility.FromJson<SerializableContentsCatalog> (json);

			foreach (var info in catalog.assetInfoTable) {
				var assetInfo = new AssetInfo ();
				assetInfo.packageName = pkgName;
				assetInfo.assetName = info.assetName;
				assetInfo.bundleName = info.bundleName;

				instance.m_assetInfoTable[new AssetID (info.assetId)] = assetInfo;
				//Debug.Log (info.assetId + " : " + pkgName + " : " + assetInfo.assetName);
			}
		}
	}
}