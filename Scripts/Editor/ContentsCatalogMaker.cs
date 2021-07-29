using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace ICKX.Kerosene {

	public class ContentsCatalogMaker {

//		[MenuItem ("ICKX/CreateCatalog")]
		public static void CreateCatalog () {
			//PkgCatalogSettingsを取得.
			var directoryPkgTable = new Dictionary<string, ContentsCatalogSettings> ();
			var directoryCatalogTable = new Dictionary<string, SerializableContentsCatalog> ();

			var guids = AssetDatabase.FindAssets ("t:ContentsCatalogSettings");
			foreach (var guid in guids) {
				string path = AssetDatabase.GUIDToAssetPath (guid);
				string dir = System.IO.Path.GetDirectoryName (path).Replace ('\\', '/');
				var settings = AssetDatabase.LoadAssetAtPath<ContentsCatalogSettings> (path);

				directoryPkgTable[dir] = settings;
				directoryCatalogTable[dir] = new SerializableContentsCatalog ();
			}

			directoryPkgTable["Default"] = null;
			directoryCatalogTable["Default"] = new SerializableContentsCatalog ();

			//アセットを検索
			var pkgDirctorys = directoryCatalogTable.Keys;
			foreach (var assetBundleName in AssetDatabase.GetAllAssetBundleNames()) {
				foreach (var path in AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName)) {
					string guid = AssetDatabase.AssetPathToGUID (path);
					string dir = System.IO.Path.GetDirectoryName (path).Replace ('\\', '/');

					var assetInfo = new SerializableContentsCatalog.AssetInfo ();
					assetInfo.assetId = guid;
					assetInfo.bundleName = assetBundleName;
					assetInfo.assetName = path.ToLower();// System.IO.Path.GetFileName (path);

					SerializableContentsCatalog catalog = null;
					foreach (var pkgDir in pkgDirctorys) {
						if( dir.Contains(pkgDir)) {
							//Debug.Log ("Pkg : " + path);
							catalog = directoryCatalogTable[pkgDir];
							catalog.assetInfoTable.Add (assetInfo);
							break;
						}
					}

					if(catalog == null) {
						//Debug.Log ("Default : " + path);
						catalog = directoryCatalogTable["Default"];
						catalog.assetInfoTable.Add (assetInfo);
					}
				}
			}

			//パッケージごとにCatalogをjson化
			foreach (var pkgDir in pkgDirctorys) {
				var settings = directoryPkgTable[pkgDir];
				string packageName = (settings == null)? "Default" : settings.PackageName;
				string folderPath = AssetBundleManager.GetAssetBundlePlatformFolderPath ();
				if(!System.IO.Directory.Exists(folderPath)) {
					System.IO.Directory.CreateDirectory (folderPath);
				}
				string filePath = folderPath + "/" + packageName + "_Catalog.json";
				var catalog = directoryCatalogTable[pkgDir];
				System.IO.File.WriteAllText (filePath, JsonUtility.ToJson (catalog, true));
			}
		}

//		[MenuItem ("ICKX/LoadCatalog")]
		public static void LoadCatalog () {
			ContentsCatalog.LoadCatalog ();
		}
	}
}
