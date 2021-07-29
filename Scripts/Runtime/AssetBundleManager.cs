using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Events;

namespace ICKX.Kerosene {


	public class AssetBundleManager : MonoBehaviour {

		public class State {
			public AssetBundle assetBundle { get; internal set; }	//Unloadしていればnull
			public bool isLoadedOwnSelf { get; internal set; }		//自身のAssetBundleNameからロードされたかどうか
			public bool isLoadCompleteDependencies { get; internal set; }	//依存するすべてのAssetBundleがロード済みかどうか

			public int referencedCount { get; internal set; }		//このAssetBundleに依存してるロード済みorロード中のAssetBundleの数
			public List<string> belongGroups { get; internal set; }	//このAssetBundleとその依存しているAssetBundleが所属するGroup

			public UpdateTask updatTask { get; internal set; }		//このAssetBundleのUpdate処理が進行してなければnull
			public CreateTask createTask { get; internal set; }    //このAssetBundleのCreate処理が進行してなければnull

			public string[] dependencies { get; internal set; }

			public UnityAction<State> onLoad { get; internal set; }
			public UnityAction<State> onUnload { get; internal set; }

			public State() {
				assetBundle = null;
				isLoadedOwnSelf = false;
				isLoadCompleteDependencies = false;
				belongGroups = new List<string>();
				updatTask = null;
				createTask = null;
				dependencies = null;
				referencedCount = 0;
				onLoad = null;
				onUnload = null;
			}

			public void OnLoadDependencies(State dependencyState) {

				if(!isLoadCompleteDependencies) {
					isLoadCompleteDependencies = true;

					if (dependencies != null && dependencies.Length > 0) {
						foreach (var dependency in dependencies) {
							State stateDependency = null;

							if (!m_AssetBundleStates.TryGetValue(dependency, out stateDependency)) {
								isLoadCompleteDependencies = false;
								return;
							}
							if (stateDependency.assetBundle == null || !stateDependency.isLoadCompleteDependencies) {
								isLoadCompleteDependencies = false;
								return;
							}
						}
					}
				}

				if (isLoadCompleteDependencies) {
					if (onLoad != null) onLoad(this);
				}
			}

			public void OnUnloadDependencies(State dependencyState) {
				isLoadCompleteDependencies = false;

				if (onUnload != null) onUnload(this);
			}
		}

		public class DownloadBuffer {
			public byte[] buffer { get; internal set; }
			public bool isUsed { get; internal set; }
		}

		public class CreateTask {
			public string assetBundleName { get; internal set; }
			public State assetBundleState { get; internal set; }
			public UnityEngine.AssetBundleCreateRequest createRequest { get; internal set; }

			public CreateTask(string assetBundleName, State assetBundleState) {
				this.assetBundleName = assetBundleName;
				this.assetBundleState = assetBundleState;
				createRequest = null;
			}

			public bool Update () {
				var state = this.assetBundleState;

				if (this.createRequest == null) {
					Log(LogType.Info, "createTask start " + this.assetBundleName);

					string filePath = GetAssetBundleFilePath(this.assetBundleName);
					this.createRequest = AssetBundle.LoadFromFileAsync(filePath);
				} else {
					if (this.createRequest.isDone) {
						Log(LogType.Info, "createTask end " + this.assetBundleName);

						state.assetBundle = this.createRequest.assetBundle;
						state.createTask = null;
						state.OnLoadDependencies(state);
						this.createRequest = null;

						return false;
					}
				}
				return true;
			}
		}

		public class UpdateTask {
			public string assetBundleName { get; internal set; }
			public State assetBundleState { get; internal set; }
			public UnityWebRequest webRequest { get; internal set; }

			public UpdateTask(string assetBundleName, State assetBundleState) {
				this.assetBundleName = assetBundleName;
				this.assetBundleState = assetBundleState;
				webRequest = null;
			}

			//falseなら処理を終了.
			public bool Update () {
				var state = this.assetBundleState;

				if (this.webRequest == null) {
					Log(LogType.Info, "updateTask start " + this.assetBundleName);

					string url = GetBaseDownloadingURL(this.assetBundleName);
					string filePath = GetAssetBundleFilePath(this.assetBundleName);

					this.webRequest = new UnityWebRequest(url);
					this.webRequest.downloadHandler = new FileDownloadHandler(filePath, GetUnusedBuffer());
					this.webRequest.SendWebRequest();
				} else {
					if (this.webRequest.isDone) {
						Log(LogType.Info, "updateTask end " + this.assetBundleName);

						this.webRequest.Dispose();
						state.updatTask = null;
						this.webRequest = null;

						return false;
					}
				}
				return true;
			}
		}

		public enum LogMode { All, JustErrors };
		public enum LogType { Info, Warning, Error };

		static List<string> m_ActiveGroupNames = new List<string>();
		//static AssetBundleManifest m_previousAssetBundleManifest = null;
		static Dictionary<string, AssetBundleManifest> m_AddonAssetBundleManifestTable = null;

		static AssetBundleManager manager = null;

		//static bool needRecheckIsLoadedDependenciesFlag = false;

#if UNITY_EDITOR
		static int m_SimulateAssetBundleInEditor = -1;
		static int m_ShowToggleProjectViewInEditor = -1;
		static int m_UseLocalBuildAssetBundleInEditor = -1;
#endif

		static Dictionary<string, State> m_AssetBundleStates = new Dictionary<string, State>();
		static List<AssetBundleLoadOperation> m_LoadOperations = new List<AssetBundleLoadOperation>();
		static List<CreateTask> m_CreateTasks = new List<CreateTask>();
		static List<UpdateTask> m_UpdateTasks = new List<UpdateTask>();
		//static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();

		static DownloadBuffer[] downloadBuffers = null;

		public static LogMode logMode { get; set; }

		public static AssetBundleManifest mainAssetBundleManifest { get; private set; }

		public static IReadOnlyDictionary<string, AssetBundleManifest> addonAssetBundleManifestTable {
			get { return m_AddonAssetBundleManifestTable; }
		}

		public static bool IsReady {
			get { return mainAssetBundleManifest != null; }
		}

		public static bool AnyAssetBundleDonwloading {
			get { return m_UpdateTasks.Count > 0; }
		}

		public static bool AnyAssetBundleLoading {
			get { return m_CreateTasks.Count > 0; }
		}

		public static string GetBaseDownloadingURL(string fileName) {
			return AssetBundleSettings.Data.serverUrl + "/" + AssetBundleUtility.GetPlatformName() + "/" + fileName;
		}

		public static string GetAssetBundleRootFolderPath () {

			if (useLocalBuildAssetBundleInEditor) {
				return System.Environment.CurrentDirectory.Replace ("\\", "/") + "/" + AssetBundleSettings.Data.directortyName;
			} else {
				return AssetBundleSettings.Data.assetBundleFolderPath + "/" + AssetBundleSettings.Data.directortyName;
			}
		}
		
		public static string GetAssetBundlePlatformFolderPath () {
			return GetAssetBundleRootFolderPath()  + "/" + AssetBundleUtility.GetPlatformName ();
		}

		public static string GetAssetBundleFilePath (string fileName) {
			return GetAssetBundlePlatformFolderPath () + "/" + fileName;
		}

		static DownloadBuffer GetUnusedBuffer() {
			if (downloadBuffers == null) {
				downloadBuffers = new DownloadBuffer[AssetBundleSettings.Data.maxParallelDownloads];
				for (int i = 0; i < downloadBuffers.Length; i++) downloadBuffers[i] = new DownloadBuffer();
			}
			var downloadBuffer = downloadBuffers.FirstOrDefault(d => !d.isUsed);
			if (downloadBuffer != null && downloadBuffer.buffer == null) {
				downloadBuffer.buffer = new byte[AssetBundleSettings.Data.downloadBufferSize * 1024];
			}
			return downloadBuffer;
		}

		private static void Log(LogType logType, string text) {
			if (logType == LogType.Error)
				Debug.LogError("[AssetBundleManager] " + text);
			else if (logMode == LogMode.All)
				Debug.Log("[AssetBundleManager] " + text);
		}

#if UNITY_EDITOR
		public static bool simulateAssetBundleInEditor {
			get {
				if (m_SimulateAssetBundleInEditor == -1)
					m_SimulateAssetBundleInEditor = EditorPrefs.GetBool ("Kerosene_SimulateAssetBundles", true) ? 1 : 0;

				return m_SimulateAssetBundleInEditor != 0;
			}
			set {
				int newValue = value ? 1 : 0;
				if (newValue != m_SimulateAssetBundleInEditor) {
					m_SimulateAssetBundleInEditor = newValue;
					EditorPrefs.SetBool ("Kerosene_SimulateAssetBundles", value);
				}
			}
		}

		public static bool showToggleProjectViewInEditor {
			get {
				if (m_ShowToggleProjectViewInEditor == -1)
					m_ShowToggleProjectViewInEditor = EditorPrefs.GetBool ("Kerosene_ShowToggleProjectView", false) ? 1 : 0;

				return m_ShowToggleProjectViewInEditor != 0;
			}
			set {
				int newValue = value ? 1 : 0;
				if (newValue != m_ShowToggleProjectViewInEditor) {
					m_ShowToggleProjectViewInEditor = newValue;
					EditorPrefs.SetBool ("Kerosene_ShowToggleProjectView", value);
				}
			}
		}
#endif
		public static bool useLocalBuildAssetBundleInEditor {
			get {
#if UNITY_EDITOR
				if (m_UseLocalBuildAssetBundleInEditor == -1)
					m_UseLocalBuildAssetBundleInEditor = EditorPrefs.GetBool("Kerosene_UseLocalBuildAssetBundle", true) ? 1 : 0;

				return m_UseLocalBuildAssetBundleInEditor != 0;
#else
				return false;
#endif
			}
#if UNITY_EDITOR
			set {
				int newValue = value ? 1 : 0;
				if (newValue != m_UseLocalBuildAssetBundleInEditor) {
					m_UseLocalBuildAssetBundleInEditor = newValue;
					EditorPrefs.SetBool("Kerosene_UseLocalBuildAssetBundle", value);
				}
			}
#endif
		}

		static private void CreateManager () {
			manager = new GameObject ("AssetBundleManager").AddComponent<AssetBundleManager> ();
			DontDestroyOnLoad (manager.gameObject);
		}

		/// <summary>
		/// AssetBundleManifestを読み込み、Managerを初期化する
		/// </summary>
		/// <param name="manifestAssetBundleName"></param>
		/// <returns></returns>
		static public void Initialize() {
			if (manager == null) {
				CreateManager ();
			}
#if UNITY_EDITOR
			if (simulateAssetBundleInEditor) return;
			Log (LogType.Info, "Simulation Mode: " + (simulateAssetBundleInEditor ? "Enabled" : "Disabled"));
#endif
			manager.StartCoroutine(LoadManifest ("Default"));
		}

		/// <summary>
		/// Manifestファイルのロードをします
		/// </summary>
		/// <param name="packageName"></param>
		/// <returns></returns>
		static public IEnumerator LoadManifest (string packageName, bool isAddOn = false) {
			
			string filePath = GetAssetBundleFilePath(packageName);

			if (!useLocalBuildAssetBundleInEditor && AssetBundleSettings.Data.IsUseServer) {
				//if (AssetBundleSettings.Data.useLoadFromCacheOrDownload) {
				//	//アップデートする前に古いManifestファイルをロードしておく.
				//	using (var webRequest = UnityWebRequest.GetAssetBundle(url)) {
				//		yield return webRequest;
				//		if (webRequest.isError) {
				//			Log(LogType.Error, "Manifest DownloadError : " + webRequest.error);
				//			yield break;
				//		}
				//		using (var handler = webRequest.downloadHandler as DownloadHandlerAssetBundle) {
				//			var loadAsync = handler.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
				//			yield return loadAsync;
				//			m_previousAssetBundleManifest = loadAsync.asset as AssetBundleManifest;
				//			handler.assetBundle.Unload(false);
				//		}
				//	}
				//	//起動時にかならずアップデート.
				//	using (var webRequest = UnityWebRequest.GetAssetBundle(url, new Hash128(), 0)) {
				//		yield return webRequest;
				//		if (webRequest.isError) {
				//			Log(LogType.Error, "Manifest DownloadError : " + webRequest.error);
				//			yield break;
				//		}
				//		using (var handler = webRequest.downloadHandler as DownloadHandlerAssetBundle) {
				//			var loadAsync = handler.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
				//			yield return loadAsync;
				//			m_AssetBundleManifest = loadAsync.asset as AssetBundleManifest;
				//			handler.assetBundle.Unload(false);
				//		}
				//	}
				//	yield break;
				//} else {
				/*
				//アップデートする前に古いManifestファイルをロードしておく.
				if (!File.Exists(filePath)) {
					//Log(LogType.Error, "Not Found AssetBundleManifest");
				} else {
					var createRequest = AssetBundle.LoadFromFileAsync(filePath);
					yield return createRequest;
					var loadAsync = createRequest.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
					yield return loadAsync;
					m_previousAssetBundleManifest = loadAsync.asset as AssetBundleManifest;
					createRequest.assetBundle.Unload(false);
				}

				//起動時にかならずアップデート.
				UnityWebRequest webRequest = new UnityWebRequest(url);
				webRequest.downloadHandler = new FileDownloadHandler(filePath, GetUnusedBuffer());
				yield return webRequest.Send();

				if(webRequest.isError) {
					Log(LogType.Error, "UnityWebRequest error : " + webRequest.error);
				}
				webRequest.Dispose();
				*/
				//}
				throw new System.NotImplementedException ();
			}
			//LoadFromFileでManifestをロードする.
			if (!File.Exists(filePath)) {
				Log(LogType.Error, "Not Found AssetBundleManifest " + filePath);
			} else
			{
				Log(LogType.Info, "Load AssetBundleManifest " + filePath);
				//LoadFromFileを使ってロード
				var createRequest = AssetBundle.LoadFromFileAsync(filePath);
				yield return createRequest;
				var loadAsync = createRequest.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
				yield return loadAsync;

				if(isAddOn) {
					m_AddonAssetBundleManifestTable[packageName] = loadAsync.asset as AssetBundleManifest;
				} else {
					mainAssetBundleManifest = loadAsync.asset as AssetBundleManifest;
				}

				ContentsCatalog.LoadCatalog (packageName);

				createRequest.assetBundle.Unload(false);
			}
		}

		/// <summary>
		/// 依存先を含めてロード済みのAssetBundleを返す
		/// </summary>
		/// <param name="assetBundleName"></param>
		/// <param name="error"></param>
		/// <returns></returns>
		static public State GetLoadedAssetBundle(string assetBundleName) {

			State state = null;
			if(!m_AssetBundleStates.TryGetValue(assetBundleName, out state)) {
				return null;
			}
			
			if(!state.isLoadedOwnSelf) {
				//Log(LogType.Info, assetBundleName + " was not Loaded...");
				return null;
			}
			if (state.assetBundle == null) {
				//Log(LogType.Info, assetBundleName + " is now loading...");
				return null;
			}
			if (!state.isLoadCompleteDependencies) {
				//Log(LogType.Info, assetBundleName + " dependencies is now loading...");
				return null;
			}
			return state;
		}

		static public State GetAssetBundleState(string assetBundleName) {
			State state = null;
			if (!m_AssetBundleStates.TryGetValue(assetBundleName, out state)) {
				return null;
			}
			return state;
		}

		/// <summary>
		/// すべてのAssetBundleをアップデートします
		/// </summary>
		/// <returns></returns>
		static public void UpdateAssetBundleAll() {
			throw new System.NotImplementedException();
			/*
#if UNITY_EDITOR
			if (SimulateAssetBundleInEditor) return;
#endif
			string error;
			foreach (var assetBundleName in m_AssetBundleManifest.GetAllAssetBundles()) {
				UpdateAssetBundleIfNeeded(assetBundleName, out error);
			}
			*/
		}

		/// <summary>
		/// サーバーで更新があるAssetBundleをアップデートします
		/// </summary>
		/// <param name="assetBundleName"></param>
		/// <returns></returns>
		static public bool UpdateAssetBundleIfNeeded(string assetBundleName, out string error) {
			throw new System.NotImplementedException();
			/*
			error = null;

#if UNITY_EDITOR
			if (SimulateAssetBundleInEditor) return true;
#endif
			if (m_previousAssetBundleManifest != null) {
				Hash128 prevHash = m_previousAssetBundleManifest.GetAssetBundleHash(assetBundleName);
				Hash128 currentHash = m_AssetBundleManifest.GetAssetBundleHash(assetBundleName);
				//Hashが変化していなければアップデートしない.
				if (currentHash == prevHash) return true;
			}

			//stateを取得 なければ生成する.
			State state = null;
			if (!m_AssetBundleStates.TryGetValue(assetBundleName, out state)) {
				state = new State();
				m_AssetBundleStates[assetBundleName] = state;
			}else {
				//すでにUpdate中ならなにもしない
				if (state.updatTask != null) {
	//				error = "This assetBundle was requested Update.";
					return false;
				}

				//AssetBundleが展開済みの場合はアップデート失敗とする
				if (state.assetBundle != null) {
					error = "This assetBundle is opened.";
					return false;
				}

				//ロードリクエストが始まっていた場合はアップデート失敗とする
				if (state.createTask != null) {
					error = "This assetBundle was requested loading.";
					return false;
				}
			}

			//アップデートリクエストを立てる (同時ダウンロード数の制限のため)
			var request = new UpdateTask(assetBundleName, state);
			m_UpdateTasks.Add(request);
			state.updatTask = request;
			//state.isLoadCompleteDependencies = false;
			return true;
			*/
		}

		/// <summary>
		/// 指定したグループのAssetBundleをUnloadします
		/// </summary>
		/// <param name="groupName"></param>
		/// <param name="unloadAllLoadedObjects"></param>
		static public bool UnloadAssetBundleGroup(string groupName, bool unloadAllLoadedObjects) {

			bool loadingAnyAssetBundles = false;
			List<string> removeList = new List<string>();

			foreach (var pair in m_AssetBundleStates) {
				if (pair.Value.belongGroups.Contains(groupName)) {
					//ほかに所属するグループがなければUnload
					if (pair.Value.belongGroups.Count == 1) {
						//ロード中ならUnloadしない
						if (pair.Value.assetBundle != null) {
							pair.Value.belongGroups.Remove(groupName);
							if (pair.Value.isLoadedOwnSelf) {
								removeList.Add(pair.Key);
							}
						} else {
							//指定したGroupの中にロードしそこねた
							loadingAnyAssetBundles = true;
						}
					} else {
						pair.Value.belongGroups.Remove(groupName);
					}
				}
			}
			for (int i = 0; i < removeList.Count; i++) {
				UnloadAssetBundle(removeList[i], unloadAllLoadedObjects);
			}

			if (!loadingAnyAssetBundles) {
				m_ActiveGroupNames.Remove(groupName);
				return true;
			} else {
				//なにかロード中であれば指定したGroupNameがアンロードしきれていない
				return false;
			}
		}

		/// <summary>
		/// 依存関係を解決しながらAssetBundleをUnloadします
		/// </summary>
		/// <param name="assetBundleName"></param>
		/// <param name="unloadAllLoadedObjects"></param>
		static public void UnloadAssetBundle(string assetBundleName, bool unloadAllLoadedObjects) {
#if UNITY_EDITOR
			if (simulateAssetBundleInEditor) return;
#endif
			State state = GetLoadedAssetBundle(assetBundleName);
			if (state == null) return;

			UnloadAssetBundleInternal(state, true, unloadAllLoadedObjects);
			UnloadAssetBundleDependencyInternal(state, unloadAllLoadedObjects);
		}

		static private void UnloadAssetBundleInternal(State state, bool removeOwnSelf, bool unloadAllLoadedObjects) {
#if UNITY_EDITOR
			if (simulateAssetBundleInEditor) return;
#endif
			if (state == null) return;
			if (removeOwnSelf) {
				state.isLoadedOwnSelf = false;
			}
			//m_ReferencedCount>0であれば他のAssetBundleに依存しているのでまだUnloadしない
			if (--state.referencedCount == 0) {
				string assetBundleName = state.assetBundle.name;
				state.assetBundle.Unload(unloadAllLoadedObjects);
				state.assetBundle = null;
				state.OnUnloadDependencies(state);

				State dependencyState;
				if (state.dependencies != null) {
					foreach (var dependency in state.dependencies) {
						if (m_AssetBundleStates.TryGetValue(dependency, out dependencyState)) {
							dependencyState.onLoad -= state.OnLoadDependencies;
							dependencyState.onUnload -= state.OnUnloadDependencies;
						}
					}
				}

				m_AssetBundleStates.Remove(assetBundleName);

				Log(LogType.Info, assetBundleName + " has been unloaded successfully");
			}
		}

		static private void UnloadAssetBundleDependencyInternal(State state, bool unloadAllLoadedObjects) {
#if UNITY_EDITOR
			if (simulateAssetBundleInEditor) return;
#endif
			if (state == null) return;
			if (state.dependencies == null) return;

			foreach (var dependency in state.dependencies) {
				State dependencyState = null;
				if (!m_AssetBundleStates.TryGetValue(dependency, out dependencyState)) continue;
				if (dependencyState.assetBundle == null || !dependencyState.isLoadCompleteDependencies) continue;

				UnloadAssetBundleInternal(dependencyState, false, unloadAllLoadedObjects);
				UnloadAssetBundleDependencyInternal(dependencyState, unloadAllLoadedObjects);
			}
		}
		
		static State CreateAssetBundle (string packageName, string assetBundleName, string groupName) {
			var state = CreateAssetBundleInternal(packageName, assetBundleName, groupName, false);
			if (!state.isLoadedOwnSelf) state.referencedCount++;

			CreateAssetBundleDependenciesInternal(packageName, state, groupName, !state.isLoadedOwnSelf);

			state.isLoadedOwnSelf = true;

			return state;
		}

		static void CreateAssetBundleDependenciesInternal(string packageName, State state, string groupName, bool incrementReferencedCount) {

			if (state.dependencies!=null && state.dependencies.Length > 0) {

				State dpState;
				foreach (var dependency in state.dependencies) {
					dpState = CreateAssetBundleInternal(packageName, dependency, groupName, incrementReferencedCount);
					if (dpState != null) {
						dpState.onLoad += state.OnLoadDependencies;
						dpState.onUnload += state.OnUnloadDependencies;

						CreateAssetBundleDependenciesInternal(packageName, dpState, groupName, incrementReferencedCount);
					}
				}
			}
		}

		static State CreateAssetBundleInternal(string packageName, string assetBundleName, string groupName, bool incrementReferencedCount) {
			State state = null;
			if (!m_AssetBundleStates.TryGetValue(assetBundleName, out state)) {
				state = new State();
				m_AssetBundleStates[assetBundleName] = state;
			}

			if (incrementReferencedCount) state.referencedCount++;

			if (state.dependencies == null) {
				if(packageName == "Default") {
					state.dependencies = mainAssetBundleManifest.GetDirectDependencies (assetBundleName);
				} else {
					state.dependencies = m_AddonAssetBundleManifestTable[packageName].GetDirectDependencies (assetBundleName);
				}
			}

			if (state.updatTask != null) {
				Log(LogType.Info, assetBundleName + " is updating. It takes a long time to load assetbundle");
			}

			if (!state.belongGroups.Contains(groupName)) {
				state.belongGroups.Add(groupName);
			}

			if (state.createTask == null && state.assetBundle == null) {
				var request = new CreateTask(assetBundleName, state);

				m_CreateTasks.Add(request);
				state.createTask = request;
			}

			return state;
		}

		static public AssetBundleLoadAssetOperation LoadAssetAsync (AssetID assetId, string groupName, System.Type type) {
			var info = ContentsCatalog.instance.assetInfoTable[assetId];
			return LoadAssetAsync (info.packageName, info.bundleName, groupName, info.assetName, type);
		}

		static public AssetBundleLoadAssetOperation LoadAssetAsync (string assetBundleName
				, string groupName, string assetName, System.Type type) {
			return LoadAssetAsync ("Default", assetBundleName, groupName, assetName, type);
		}

		static public AssetBundleLoadAssetOperation LoadAssetAsync(string packageName, string assetBundleName
				, string groupName, string assetName, System.Type type) {

			Log (LogType.Info, "Loading " + assetName + " from " + assetBundleName + " bundle");
			
#if UNITY_EDITOR
			if (simulateAssetBundleInEditor) {
				string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, assetName);
				if (assetPaths.Length == 0) {
					Log(LogType.Error, "There is no asset with name \"" + assetName + "\" in " + assetBundleName);
					return null;
				}
				var operation = new AssetBundleLoadAssetOperationSimulation();
				Object target = AssetDatabase.LoadMainAssetAtPath(assetPaths[0]);
				operation.SetAssetInternal(target);
				return operation;
			} else
#endif
			{
				State state = CreateAssetBundle(packageName, assetBundleName, groupName);
				var operation = new AssetBundleLoadAssetOperationFull(state, assetName, type);
				m_LoadOperations.Add(operation);

				if (!m_ActiveGroupNames.Contains(groupName)) {
					m_ActiveGroupNames.Add(groupName);
				}
				return operation;
			}
		}

		static public AssetBundleLoadLevelOperation LoadLevelAsync (AssetID assetId, string groupName, LoadSceneMode loadSceneMode, bool allowSceneActivation = true) {
			var info = ContentsCatalog.instance.assetInfoTable[assetId];
			return LoadLevelAsync (info.packageName, info.bundleName, groupName, info.assetName, loadSceneMode, allowSceneActivation);
		}

		static public AssetBundleLoadLevelOperation LoadLevelAsync (string assetBundleName
				, string groupName, string levelName, LoadSceneMode loadSceneMode, bool allowSceneActivation = true) {
			return LoadLevelAsync ("Default", assetBundleName, groupName, levelName, loadSceneMode, allowSceneActivation);
		}

		static public AssetBundleLoadLevelOperation LoadLevelAsync(string packageName, string assetBundleName
				, string groupName, string levelName, LoadSceneMode loadSceneMode, bool allowSceneActivation = true) {

			Log(LogType.Info, "Loading " + levelName + " from " + assetBundleName + " bundle");

#if UNITY_EDITOR
			if (simulateAssetBundleInEditor) {
				string[] levelPaths = UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, levelName);
				if (levelPaths.Length == 0) {
					Log(LogType.Error, "There is no scene with name \"" + levelName + "\" in " + assetBundleName);
					return null;
				}

				var operation = new AssetBundleLoadLevelOperationSimulation();
				AsyncOperation levelOperation;
				var parm = new LoadSceneParameters (loadSceneMode);
				levelOperation = EditorSceneManager.LoadSceneAsyncInPlayMode (levelPaths[0], parm);
				operation.SeAsyncOperationInternal(levelOperation);
				return operation;
			} else
#endif
			{
				State state = CreateAssetBundle(packageName, assetBundleName, groupName);
				var operation = new AssetBundleLoadLevelOperationFull(state, levelName, loadSceneMode, allowSceneActivation);
				m_LoadOperations.Add(operation);

				if (!m_ActiveGroupNames.Contains(groupName)) {
					m_ActiveGroupNames.Add(groupName);
				}
				return operation;
			}
		}

		void Update() {
			CheckAssetBundleUpdateRequest();
			CheckAssetBundleCreateRequest();
			CheckAssetBundleLoadOperation();
		}

		//AssetBundleのアップデートリクエストを処理.
		void CheckAssetBundleUpdateRequest() {
			int count = -1;
			while (count + 1 < m_UpdateTasks.Count) {
				count++;
				//同時ダウンロード数に制限.
				if (count < AssetBundleSettings.Data.maxParallelDownloads) {
					var updateTask = m_UpdateTasks[count];
					var state = updateTask.assetBundleState;

					if (!updateTask.Update()) {
						//UpdateがfalseならTaskをリストから削除.
						m_UpdateTasks.RemoveAt(count);
						if (state.createTask == null) {
							m_AssetBundleStates.Remove(updateTask.assetBundleName);
						}
						count--;
					}
				}
			}
		}

		void CheckAssetBundleCreateRequest() {
			int count = -1;
			int skipCount = 0;
			while (count + 1 < m_CreateTasks.Count) {
				count++;
				//同時ファイル読み込みに制限
				if (count - skipCount < AssetBundleSettings.Data.maxParallelDownloads) {
					var createTask = m_CreateTasks[count];
					var state = createTask.assetBundleState;

					//ファイルがアップデート中なら待機.
					if(state.updatTask != null) {
						skipCount++;
						continue;
					}

					if (!createTask.Update()) {
						//UpdateがfalseならTaskをリストから削除.
						m_CreateTasks.RemoveAt(count);
						count--;
					}
				}
			}
		}

		void CheckAssetBundleLoadOperation() {
			int count = -1;
			while (count + 1 < m_LoadOperations.Count) {
				count++;

				if (!m_LoadOperations[count].Update()) {
					//Update処理が必要なくなった段階でm_LoadOperationsから削除.
					Log(LogType.Info, "m_LoadOperations update finish");
					m_LoadOperations.RemoveAt(count);
					count--;
				}
			}
		}


#if UNITY_EDITOR
		[CustomEditor(typeof(AssetBundleManager))]
		public partial class AssetBundleManagerEditor : Editor {

			bool showOnGUIAssetBundleStates = false;
			bool showOnGUIUpdateTasks = false;
			bool showOnGUICreateTasks = false;

			private void OnEnable() {
				showOnGUIAssetBundleStates = EditorPrefs.GetBool("showOnGUIAssetBundleStates", false);
				showOnGUIUpdateTasks = EditorPrefs.GetBool("showOnGUIUpdateTasks", false);
				showOnGUICreateTasks = EditorPrefs.GetBool("showOnGUICreateTasks", false);
			}

			private void OnDisable() {
				EditorPrefs.SetBool("showOnGUIAssetBundleStates", showOnGUIAssetBundleStates);
				EditorPrefs.SetBool("showOnGUIUpdateTasks", showOnGUIUpdateTasks);
				EditorPrefs.SetBool("showOnGUICreateTasks", showOnGUICreateTasks);
			}

			public override void OnInspectorGUI() {
				base.OnInspectorGUI();

				//var manager = target as ICKX.Kerosene.AssetBundleManager;
				//AssetBundleManager.m_UpdateTasks;
				//AssetBundleManager.m_CreateTasks;
				//AssetBundleManager.m_Dependencies;

				showOnGUIAssetBundleStates = EditorGUILayout.Foldout(showOnGUIAssetBundleStates, "AssetBundleStates");
				if (showOnGUIAssetBundleStates) {
					OnGUIAssetBundleStates();
				}
				showOnGUIUpdateTasks = EditorGUILayout.Foldout(showOnGUIUpdateTasks, "UpdateTasks");
				if (showOnGUIUpdateTasks) {
					OnGUIUpdateTasks();
				}
				showOnGUICreateTasks = EditorGUILayout.Foldout(showOnGUICreateTasks, "CreateTasks");
				if (showOnGUICreateTasks) {
					OnGUICreateTasks();
				}
			}

			void OnGUIAssetBundleStates () {
				EditorGUI.indentLevel++;
				using (var scopeV = new EditorGUILayout.VerticalScope("box")) {
					foreach (var pair in m_AssetBundleStates) {
						using (var scopeH = new EditorGUILayout.HorizontalScope()) {
							EditorGUILayout.PrefixLabel("AssetBundleName");
							EditorGUILayout.TextField(pair.Key);
						}
						EditorGUI.indentLevel++;
						{
							using (var scopeH2 = new EditorGUILayout.HorizontalScope()) {
								EditorGUILayout.PrefixLabel("IsLoadedOwnSelf");
								EditorGUILayout.Toggle(pair.Value.isLoadedOwnSelf);
							}
							using (var scopeH2 = new EditorGUILayout.HorizontalScope()) {
								EditorGUILayout.PrefixLabel("IsLoaded");
								EditorGUILayout.Toggle(pair.Value.assetBundle);
							}
							using (var scopeH2 = new EditorGUILayout.HorizontalScope()) {
								EditorGUILayout.PrefixLabel("IsLoadedDependencies");
								EditorGUILayout.Toggle(pair.Value.isLoadCompleteDependencies);
								//int dpCount = (pair.Value.dependencies != null ? pair.Value.dependencies.Length : 0);
								//EditorGUILayout.TextField(pair.Value.loadedDependenciesCount + "/" + dpCount);
							}
							using (var scopeH2 = new EditorGUILayout.HorizontalScope()) {
								EditorGUILayout.PrefixLabel("ReferencedCount");
								EditorGUILayout.IntField(pair.Value.referencedCount);
							}
							using (var scopeH2 = new EditorGUILayout.HorizontalScope()) {
								EditorGUILayout.PrefixLabel("BelongGroups");
								EditorGUILayout.TextField(string.Join(",", pair.Value.belongGroups.ToArray() ));
							}
						}
						EditorGUI.indentLevel--;
					}
				}
				EditorGUI.indentLevel--;
			}

			void OnGUIUpdateTasks() {
				EditorGUI.indentLevel++;
				using (var scopeV = new EditorGUILayout.VerticalScope("box")) {
					foreach (var updateTask in m_UpdateTasks) {
						using (var scopeH = new EditorGUILayout.HorizontalScope()) {
							EditorGUILayout.PrefixLabel(updateTask.assetBundleName);
							if (updateTask.webRequest == null) {
								EditorGUILayout.Slider(0.0f, 0.0f, 1.0f);
							} else {
								EditorGUILayout.Slider(updateTask.webRequest.downloadProgress, 0.0f, 1.0f);
							}
						}
						EditorGUI.indentLevel--;
					}
				}
				EditorGUI.indentLevel--;
			}

			void OnGUICreateTasks() {
				EditorGUI.indentLevel++;
				using (var scopeV = new EditorGUILayout.VerticalScope("box")) {
					foreach (var createTask in m_CreateTasks) {
						using (var scopeH = new EditorGUILayout.HorizontalScope()) {
							EditorGUILayout.PrefixLabel(createTask.assetBundleName);
							if (createTask.createRequest == null) {
								EditorGUILayout.Slider(0.0f, 0.0f, 1.0f);
							} else {
								EditorGUILayout.Slider(createTask.createRequest.progress, 0.0f, 1.0f);
							}
						}
						EditorGUI.indentLevel--;
					}
				}
				EditorGUI.indentLevel--;
			}
		}
#endif
	}
}
 