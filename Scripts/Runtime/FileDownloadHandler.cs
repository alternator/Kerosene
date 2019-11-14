using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace ICKX.Kerosene {

	public class FileDownloadHandler : DownloadHandlerScript {

		FileStream fs;
		public int offset { get; private set; }
		public int length { get; private set; }

		AssetBundleManager.DownloadBuffer downloadBuffer;

		public FileDownloadHandler(string path, AssetBundleManager.DownloadBuffer downloadBuffer) : base(downloadBuffer.buffer) {

			Directory.CreateDirectory(Path.GetDirectoryName(path));

			if (File.Exists(path)) File.Delete(path);

			fs = new FileStream(path, FileMode.Create, FileAccess.Write);
			this.downloadBuffer = downloadBuffer;
			downloadBuffer.isUsed = true;
		}

		protected override bool ReceiveData(byte[] data, int dataLength) {
			fs.Write(data, 0, dataLength);
			offset += dataLength;
			return true;
		}

		protected override void CompleteContent() {
			fs.Flush();
			fs.Close();
			downloadBuffer.isUsed = false;
			downloadBuffer = null;
		}

		protected override void ReceiveContentLength(int contentLength) {
			length = contentLength;
		}

		//protected override float GetProgress() {
		//	if (length == 0)
		//		return 0.0f;

		//	return (float)offset / length;
		//}
	}
}