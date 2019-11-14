using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ICKX.Kerosene {
	//[CreateAssetMenu(menuName = "ScriptableObject/AssetBundleEditorSettings", fileName = "AssetBundleEditorSettings.asset")]
	public class AssetBundleEditorSettings : ScriptableObject {
		#region Data

		static private AssetBundleEditorSettings m_Data;

		static public AssetBundleEditorSettings Data {
			get {
				if (m_Data == null) {
					string guid = AssetDatabase.FindAssets("t:AssetBundleEditorSettings").FirstOrDefault();
					if (string.IsNullOrEmpty(guid)) {
						Debug.LogError("Not found");
					} else {
						m_Data = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AssetBundleEditorSettings;
					}
				}
				return m_Data;
			}
		}

		#endregion

		//	[Header("AssetBundle Settings")]
		//	public string assetBundleSaveFolder = "AssetBundles";
	}
}