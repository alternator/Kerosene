using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ICKX.Kerosene {

	[System.Serializable]
	public class AssetReference {
		public AssetID assetID;

		public event UnityAction<Object> onComplate = null;

		public Object asset { get; private set; }

		public bool IsLoad { get { return asset != null; } }

		public AssetReference (string hexGUID) {
			assetID = new AssetID (hexGUID);
		}

		public IEnumerator Load<Type> (string groupName) {
			var loadReq = AssetBundleManager.LoadAssetAsync (assetID, groupName, typeof (Type));
			yield return loadReq;

			asset = loadReq.asset;
			onComplate?.Invoke (asset);
		}

#if UNITY_EDITOR
		[CustomPropertyDrawer (typeof (AssetReference))]
		public class AssetReferenceDrawer : PropertyDrawer {

			static byte[] byteGuid = null;
			static bool ShowGUID = false;

			public override void OnGUI (Rect position, SerializedProperty property, GUIContent label) {

				if (byteGuid == null) {
					byteGuid = new byte[16];
				}

				var spropAssetID = property.FindPropertyRelative ("assetID");

				for (int i = 0; i < 16; i++) {
					var guidProp = spropAssetID.FindPropertyRelative ("b" + i);
					byteGuid[i] = (byte)guidProp.intValue;
				}
				string guid = AssetID.ByteToHexString (byteGuid);
				string path = AssetDatabase.GUIDToAssetPath (guid);

				position.width -= 50.0f;

				if (ShowGUID) {
					string tempText = EditorGUI.TextField (position, label, guid);
					if (tempText != guid && tempText.Length == 32) {
						guid = tempText;
						UpdateGUID (spropAssetID, guid);
					}
				} else {
					Object objRef = null, newObj = null;

					if (string.IsNullOrEmpty (path)) {
						newObj = EditorGUI.ObjectField (position, label, null, typeof (Object), false);
					} else {
						objRef = AssetDatabase.LoadMainAssetAtPath (AssetDatabase.GUIDToAssetPath (guid));
						newObj = EditorGUI.ObjectField (position, label, objRef, typeof (Object), false);
					}
					if (newObj == null) {
						for (int i = 0; i < 16; i++) {
							var guidProp = spropAssetID.FindPropertyRelative ("b" + i);
							guidProp.intValue = 0;
						}
					} else if (newObj != objRef) {
						long localId;
						if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier (newObj, out guid, out localId)) {
							UpdateGUID (spropAssetID, guid);
						}
					}
				}

				position.x += position.width + 5.0f;
				position.width = 45.0f;

				ShowGUID = GUI.Toggle (position, ShowGUID, "GUID", EditorStyles.miniButton);
			}

			public void UpdateGUID (SerializedProperty spropAssetID, string guid) {
				var newAssetID = AssetID.HexToAssetID (guid);

				for (int i = 0; i < 16; i++) {
					var guidProp = spropAssetID.FindPropertyRelative ("b" + i);
					guidProp.intValue = newAssetID[i];
				}
			}

			public override float GetPropertyHeight (SerializedProperty property, GUIContent label) {
				return EditorGUIUtility.singleLineHeight;
			}
		}
#endif
	}

}