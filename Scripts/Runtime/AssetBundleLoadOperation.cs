using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;

namespace ICKX.Kerosene {

	public abstract class AssetBundleLoadOperation : IEnumerator {
		public object Current {
			get {
				return null;
			}
		}
		public bool MoveNext() {
			return !IsDone();
		}

		public void Reset() {
		}

		//コルーチンの継続判定
		abstract public bool IsDone();

		abstract public float progress {
			get;
		}

		// falseなら更新対象から外れる
		abstract public bool Update();
	}

	public abstract class AssetBundleLoadLevelOperation : AssetBundleLoadOperation {
		public AsyncOperation operation { get; protected set; }
		public string error { get; protected set; }

		/// <summary>
		/// allowSceneActivation=falseの場合は0.9で止まるので注意.
		/// </summary>
		public override float progress {
			get {
				if (operation != null) {
					return operation.progress;
				} else {
					return 0.0f;
				}
			}
		}

		/// <summary>
		/// SceneのActivate完了までfalse
		/// </summary>
		/// <returns></returns>
		public override bool IsDone() {
			return (operation != null && operation.isDone);
		}

	}
	public class AssetBundleLoadLevelOperationSimulation : AssetBundleLoadLevelOperation {

		public override bool Update() {
			if (operation != null) {
				return false;
			} else {
				return true;
			}
		}

		public void SeAsyncOperationInternal (AsyncOperation operation) {
			this.operation = operation;
		}
	}

	public class AssetBundleLoadLevelOperationFull : AssetBundleLoadLevelOperation {
		public AssetBundleManager.State assetBundleState { get; protected set; }
		public string levelName { get; protected set; }
		public LoadSceneMode loadSceneMode { get; protected set; }
		public bool allowSceneActivation { get; protected set; }

		public AssetBundleLoadLevelOperationFull(AssetBundleManager.State assetBundleState
				, string levelName, LoadSceneMode loadSceneMode, bool allowSceneActivation = true) {

			this.assetBundleState = assetBundleState;
			this.levelName = levelName;
			this.loadSceneMode = loadSceneMode;
			this.allowSceneActivation = allowSceneActivation;
			error = null;
			operation = null;
		}

		public override bool Update () {
			if (operation != null) {
				return false;
			}

			if (assetBundleState.assetBundle != null && assetBundleState.isLoadCompleteDependencies) {
				if(operation == null) {
					operation = SceneManager.LoadSceneAsync(levelName, loadSceneMode);
					operation.allowSceneActivation = allowSceneActivation;
				}else {
					//assetBundleState = null;
				}
			}
			
			if (operation != null) {
				return false;
			}else {
				return true;
			}
		}
	}

	public abstract class AssetBundleLoadAssetOperation : AssetBundleLoadOperation {

		public UnityEngine.Object asset { get; protected set; }
		public string error { get; protected set; }

		public override bool IsDone() {
			return (asset != null);
		}

		public T GetAsset<T>() where T : UnityEngine.Object {
			if (asset != null) {
				return asset as T;
			} else {
				return null;
			}
		}
	}

	public class AssetBundleLoadAssetOperationSimulation : AssetBundleLoadAssetOperation {

		public override float progress { get { return 1.0f; } }

		public override bool Update() {
			if (asset != null) {
				return false;
			} else {
				return true;
			}
		}

		public void SetAssetInternal(UnityEngine.Object asset) {
			this.asset = asset;
		}
	}

	public class AssetBundleLoadAssetOperationFull : AssetBundleLoadAssetOperation {

		public AssetBundleManager.State assetBundleState { get; protected set; }
		public string assetName { get; protected set; }
		public System.Type type { get; protected set; }

		public AssetBundleRequest assetBundleRequest { get; private set; }

		public override float progress {
			get {
				if(asset != null) {
					return 1.0f;
				} if (assetBundleRequest != null) {
					return assetBundleRequest.progress;
				} else {
					return 0.0f;
				}
			}
		}

		public AssetBundleLoadAssetOperationFull(AssetBundleManager.State assetBundleState, string levelName, System.Type type) {

			this.assetBundleState = assetBundleState;
			this.assetName = levelName;
			this.type = type;
			assetBundleRequest = null;
			error = null;
			asset = null;
		}

		public override bool IsDone() {
			return (asset != null);
		}

		public override bool Update() {
			if (asset != null) {
				return false;
			}

			if (assetBundleRequest == null) {
				if (assetBundleState.assetBundle != null && assetBundleState.isLoadCompleteDependencies) {
					assetBundleRequest = assetBundleState.assetBundle.LoadAssetAsync(assetName, type);
				}
			}else {
				if(assetBundleRequest.isDone) {
					asset = assetBundleRequest.asset;
					assetBundleState = null;
					assetBundleRequest = null;
				}
			}

			if (asset != null) {
				return false;
			} else {
				return true;
			}
		}
	}
}
