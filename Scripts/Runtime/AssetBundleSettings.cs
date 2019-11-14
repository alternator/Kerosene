using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ICKX.Kerosene {

	//[CreateAssetMenu( menuName = "ScriptableObject/AssetBundleSettings", fileName= "AssetBundleSettings.asset") ]
	public class AssetBundleSettings : ScriptableObject {
		#region Data
		static private AssetBundleSettings m_Data;

		static public AssetBundleSettings Data {
			get {
				if (m_Data == null) {
					m_Data = Resources.Load<AssetBundleSettings> ("AssetBundleSettings");
				}
				return m_Data;
			}
		}
		#endregion

		public int downloadBufferSize = 512;
		public int maxParallelDownloads = 4;
		//public int maxParallelDecryption = 2;

		//[Header("Encryption Settings")]
		//public string aesKey         = "";
		//public string aesInitVector  = "";

		//public int   keySize         = 128;//bit
		//public int   blockSize       = 128;//bit

		[Header("AssetBundle Settings")]
	
		// 空であればserverからアップデートしない
		public string serverUrl = "";

		// AssetBundleをビルド先のフォルダ名.
		public string directortyName = "AssetBundles";

		// UnityWebRequest.GetAssetBundleを利用する場合はtrue
		//public bool useLoadFromCacheOrDownload = false;

		//AssetBundleの保存先
		public LoadPathType downloadFilePathType = LoadPathType.PersistentDataPath;

		[System.NonSerialized]
		private string m_assetBundleFolderPath;

		public string assetBundleFolderPath {
			get { return m_assetBundleFolderPath; }
			set {
				m_assetBundleFolderPath = value;
				downloadFilePathType = LoadPathType.Manual;
			}
		}

		public bool IsUseServer {
			get { return !string.IsNullOrEmpty(serverUrl); }
		}

		private void OnEnable() {
			//if (!Application.isPlaying) return;
			switch (downloadFilePathType) {
				case LoadPathType.CurrentDirectoryPath:
					m_assetBundleFolderPath = System.Environment.CurrentDirectory.Replace("\\", "/");
					break;
				case LoadPathType.PersistentDataPath:
					m_assetBundleFolderPath = Application.persistentDataPath;
					break;
				case LoadPathType.TemporaryCachePath:
					m_assetBundleFolderPath = Application.temporaryCachePath;
					break;
				case LoadPathType.StreamingAssetsPath:
					m_assetBundleFolderPath = Application.streamingAssetsPath;
					break;
				case LoadPathType.Manual:
					break;
			}
			//if (downloadFilePathType != LoadPathType.Manual) {
			//	m_assetBundleFolderPath += "/" + directortyName;
			//}
		}

		private void OnValidate () {
			//if (System.Text.Encoding.UTF8.GetBytes (aesKey).Length > keySize/8) {
			//	Debug.LogError ("aesKey < keySize");
			//}
			//if (System.Text.Encoding.UTF8.GetBytes (aesInitVector).Length > blockSize/8) {
			//	Debug.LogError ("aesInitVector < blockSize");
			//}
		}
	}

	public enum LoadPathType {
		CurrentDirectoryPath,
		PersistentDataPath,
		TemporaryCachePath,
		StreamingAssetsPath,
		Manual,
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(AssetBundleSettings), true)]
	public class CustomEditorAssetBundleSettings : Editor {

		public override void OnInspectorGUI() {

			EditorGUI.BeginChangeCheck();

			using (var scopeH = new EditorGUILayout.HorizontalScope()) {
				EditorGUILayout.PropertyField(serializedObject.FindProperty("downloadBufferSize"));
				EditorGUILayout.LabelField("KB", GUILayout.Width(20));
			}

			EditorGUILayout.PropertyField(serializedObject.FindProperty("maxParallelDownloads"));
			//EditorGUILayout.PropertyField(serializedObject.FindProperty("maxParallelDecryption"));
			//EditorGUILayout.PropertyField(serializedObject.FindProperty("maxParallelFileIO"));

			EditorGUILayout.PropertyField(serializedObject.FindProperty("serverUrl"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("directortyName"));

			//EditorGUILayout.PropertyField(serializedObject.FindProperty("useLoadFromCacheOrDownload"));

			string serverUrl = serializedObject.FindProperty("serverUrl").stringValue;
			//bool useLoadFromCacheOrDownload = serializedObject.FindProperty("useLoadFromCacheOrDownload").boolValue;

			//if(!useLoadFromCacheOrDownload) {
			EditorGUILayout.PropertyField(serializedObject.FindProperty("downloadFilePathType"));
			//}

			if(EditorGUI.EndChangeCheck()) {
				serializedObject.ApplyModifiedProperties();
			}
		}
	}
#endif
}