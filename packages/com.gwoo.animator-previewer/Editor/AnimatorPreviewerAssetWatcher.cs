using System;
using System.Collections.Generic;
using UnityEditor;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Global asset change tracker for the AnimatorPreviewer.
	/// Emits "versioned" batches of paths changed through import/delete/move.
	/// </summary>
	internal sealed class AnimatorPreviewerAssetWatcher : AssetPostprocessor
	{
		private readonly struct Batch
		{
			public readonly int version;
			public readonly HashSet<string> paths;

			public Batch(int version, HashSet<string> paths)
			{
				this.version = version;
				this.paths = paths;
			}
		}

		private static int _assetVersion;

		private static readonly Queue<Batch> BATCH_CHANGE_QUEUE = new();
		private const int MAX_BATCH_COUNT = 32;

		internal static int CurrentVersion => _assetVersion;

		internal static bool TryCollectChangesSince(int lastSeenVersion, out int newVersion, out HashSet<string> changedPaths)
		{
			newVersion = _assetVersion;
			changedPaths = null;

			if (newVersion == lastSeenVersion)
				return false;

			HashSet<string> merged = new(StringComparer.OrdinalIgnoreCase);

			foreach (Batch b in BATCH_CHANGE_QUEUE)
			{
				if (b.version <= lastSeenVersion || b.paths == null)
					continue;

				foreach (string p in b.paths)
					merged.Add(p);
			}

			if (merged.Count == 0)
				return false;

			changedPaths = merged;
			return true;
		}

		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);

			AddAll(set, importedAssets);
			AddAll(set, deletedAssets);
			AddAll(set, movedAssets);
			AddAll(set, movedFromAssetPaths);

			if (set.Count == 0)
				return;

			_assetVersion++;

			BATCH_CHANGE_QUEUE.Enqueue(new Batch(_assetVersion, set));
			while (BATCH_CHANGE_QUEUE.Count > MAX_BATCH_COUNT)
				BATCH_CHANGE_QUEUE.Dequeue();
		}

		private static void AddAll(HashSet<string> set, string[] arr)
		{
			if (arr == null)
				return;

			for (int i = 0; i < arr.Length; i++)
			{
				string path = arr[i];
				if (!string.IsNullOrEmpty(path))
					set.Add(path);
			}
		}
	}
}

