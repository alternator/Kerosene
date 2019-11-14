using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SerializableCollections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ICKX.Kerosene {

	[System.Serializable]
	public struct AssetID {

		[SerializeField]
		private byte b0, b1, b2, b3, b4, b5, b6, b7
					, b8, b9, b10, b11, b12, b13, b14, b15;

		[HideInInspector]
		[SerializeField]
		private int hashCode;

		public byte this[int i] {
			set {
				switch (i) {
					case 0: b0 = value; break;
					case 1: b1 = value; break;
					case 2: b2 = value; break;
					case 3: b3 = value; break;
					case 4: b4 = value; break;
					case 5: b5 = value; break;
					case 6: b6 = value; break;
					case 7: b7 = value; break;
					case 8: b8 = value; break;
					case 9: b9 = value; break;
					case 10: b10 = value; break;
					case 11: b11 = value; break;
					case 12: b12 = value; break;
					case 13: b13 = value; break;
					case 14: b14 = value; break;
					case 15: b15 = value; break;
				}
			}
			get {
				switch (i) {
					case 0: return b0;
					case 1: return b1;
					case 2: return b2;
					case 3: return b3;
					case 4: return b4;
					case 5: return b5;
					case 6: return b6;
					case 7: return b7;
					case 8: return b8;
					case 9: return b9;
					case 10: return b10;
					case 11: return b11;
					case 12: return b12;
					case 13: return b13;
					case 14: return b14;
					case 15: return b15;
				}
				throw new System.IndexOutOfRangeException ();
			}
		}

		public AssetID (string hexString = null) {
			if (string.IsNullOrEmpty (hexString)) {
				b0 = b1 = b2 = b3 = b4 = b5 = b6 = b7 = b8
					= b9 = b10 = b11 = b12 = b13 = b14 = b15 = 0;
				hashCode = 0;
			} else {
				HexToAssetID (hexString, out this);
				hashCode = ComputeHashCode (ref this);
			}
		}

		public static AssetID Generate () {
			return new AssetID (System.Guid.NewGuid ().ToString ("N"));
		}

		static int ComputeHashCode (ref AssetID assetID) {
			var hashCode = 0;
			for (var i = 0; i < 16; i++) {
				hashCode = (hashCode << 3) | (hashCode >> (29)) ^ assetID[i];
			}
			return hashCode;
		}

		public override int GetHashCode () {
			return hashCode;
		}

		public override bool Equals (object obj) {
			AssetID assetId = (AssetID)obj;
			for (int i = 0; i < 16; i++) {
				if (this[i] != assetId[i]) {
					return false;
				}
			}
			return true;
		}

		public override string ToString () {
			return b0 + "" + b1 + "" + b2 + "" + b3 + "" + b4 + "" + b5 + "" + b6 + "" + b7
					+ "" + b8 + "" + b9 + "" + b10 + "" + b11 + "" + b12 + "" + b13 + "" + b14 + "" + b15;
		}

		private const string LowerhexChars = "0123456789abcdef";
		private static string[] lowerHexBytes;

		public static AssetID HexToAssetID (string hexString) {
			AssetID assetId;
			if (!HexToAssetID (hexString, out assetId)) {
				assetId = new AssetID ();
			}
			return assetId;
		}

		public static bool HexToAssetID (string hexString, out AssetID assetId) {
			assetId = new AssetID ();

			if (hexString.Length != 32) {
				return false;
			}

			for (int i = 0; i < 16; i++) {
				int high = ParseNybble (hexString[i * 2]);
				int low = ParseNybble (hexString[i * 2 + 1]);
				assetId[i] = (byte)((high << 4) | low);
			}

			return true;
		}

		private static int ParseNybble (char c) {
			unchecked {
				uint i = (uint)(c - '0');
				if (i < 10)
					return (int)i;
				i = ((uint)c & ~0x20u) - 'A';
				if (i < 6)
					return (int)i + 10;
				throw new System.ArgumentException ("Invalid nybble: " + c);
			}
		}


		public static string ByteToHexString (byte[] value, char[] tempChars = null) {
			return ByteToHexString (value, LowerhexChars, tempChars);
		}

		private static string ByteToHexString (byte[] value, string hexChars, char[] tempChars = null) {
			if (tempChars == null) {
				tempChars = new char[value.Length * 2];
			}
			//		var hex = new char[value.Length * 2];
			int j = 0;

			for (var i = 0; i < value.Length; i++) {
				var b = value[i];
				tempChars[j++] = hexChars[b >> 4];
				tempChars[j++] = hexChars[b & 15];
			}

			return new string (tempChars);
		}

#if UNITY_EDITOR
		[CustomPropertyDrawer (typeof (AssetID))]
		public class AssetIDDrawer : PropertyDrawer {

			static byte[] byteGuid = null;

			public override void OnGUI (Rect position, SerializedProperty property, GUIContent label) {
				if (byteGuid == null) {
					byteGuid = new byte[16];
				}
				for (int i = 0; i < 16; i++) {
					var guidProp = property.FindPropertyRelative ("b" + i);
					byteGuid[i] = (byte)guidProp.intValue;
				}
				EditorGUI.TextField (position, label, ByteToHexString (byteGuid));
			}

			public override float GetPropertyHeight (SerializedProperty property, GUIContent label) {
				return EditorGUIUtility.singleLineHeight;
			}
		}
#endif
	}
}