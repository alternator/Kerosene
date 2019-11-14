using UnityEngine;
using System.Collections;
using ICKX.Kerosene;

public class KeroseneTest : MonoBehaviour {

	public AssetID assetId = AssetID.Generate();
	public AssetID assetId1 = AssetID.Generate ();
	public AssetID assetId2 = AssetID.Generate ();

	public AssetReference m_AssetReference = new AssetReference ("308f2e0a94e78104ea3c5c0d03a71556");

	private void OnGUI () {

		GUILayout.Label ("AssetBundleManifestObject : " + AssetBundleManager.AssetBundleManifestObject);

		if (GUILayout.Button ("DumpAssetBundleInfo")) {
			DumpAssetBundleInfo ();
		}
	}

	private IEnumerator Start() {
		ContentsCatalog.LoadCatalog ();
		AssetBundleManager.Initialize();

		while (AssetBundleManager.AssetBundleManifestObject == null) {
			yield return null;
		}
		DumpAssetBundleInfo ();

		assetId = AssetID.Generate();

	}

	private void Update () {
		if (Input.GetKeyDown (KeyCode.L)) {
			AssetBundleManager.LoadLevelAsync ("scene/title.bundle", "", "Title", UnityEngine.SceneManagement.LoadSceneMode.Single);
		}
		if (Input.GetKeyDown (KeyCode.M)) {
			AssetBundleManager.LoadAssetAsync ("TestPackage", "assets/_testcompany/testpackage/new material.mat.bundle", "", "New Material.mat", typeof(Material));
		}
	}

	private void DumpAssetBundleInfo () {
		string content = "Name,Hash\n";
		foreach(var bundleName in AssetBundleManager.AssetBundleManifestObject.GetAllAssetBundles()) {
			var hash = AssetBundleManager.AssetBundleManifestObject.GetAssetBundleHash(bundleName);
			content += bundleName + "," + hash + "\n";
			Debug.Log (bundleName + ", has ; " + hash + "\n GetAllDependencies\n"
				+ string.Join ("\n", AssetBundleManager.AssetBundleManifestObject.GetAllDependencies (bundleName)));
		}
		System.IO.File.WriteAllText(/*System.DateTime.Now.ToString("yymmdd_hhmmss") +*/ "_BundleInfo.csv", content);
	}
}
