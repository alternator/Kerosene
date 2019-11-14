using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ICKX.Kerosene {
	
	public static class AssetBundleLoadAssetOperationAwaitable {

		public static AssetBundleLoadAssetOperationAwaiter GetAwaiter (this AssetBundleLoadAssetOperation request) {
			return new AssetBundleLoadAssetOperationAwaiter (request);
		}
	}

	public class AssetBundleLoadAssetOperationAwaiter : INotifyCompletion {

		public AssetBundleLoadAssetOperation operation { get; private set; }
		private System.Action continuation;

		public AssetBundleLoadAssetOperationAwaiter (AssetBundleLoadAssetOperation request) {
			this.operation = request;
		}

		public void OnCompleted (System.Action continuation) {
			this.continuation = continuation;
			CoroutineManager.Start (WrappedCoroutine ());
		}

		IEnumerator WrappedCoroutine () {
			yield return operation;
			continuation ();
		}

		public bool IsCompleted {
			get {
				return operation.IsDone();
			}
		}

		public void GetResult () { }
	}
}