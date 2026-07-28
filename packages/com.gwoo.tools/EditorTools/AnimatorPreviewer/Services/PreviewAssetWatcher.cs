using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Monitors relevant assets (Controller, Clips) for changes to trigger rebinding or UI refreshes.
	/// </summary>
	internal sealed class PreviewAssetWatcher
	{
		#region Fields

		private readonly AnimatorPreviewerState _previewerState;
		private readonly AnimatorPreviewerRuntime _previewerRuntime;
		private readonly PreviewInvalidation _invalidation;
		private readonly PreviewHub _hub;

		private readonly HashSet<string> _watchedAssetPaths = new(StringComparer.OrdinalIgnoreCase);
		private int _lastSeenVersion;

		private readonly DelayedAction _delayedRebind;

		#endregion Fields

		#region Constructors

		internal PreviewAssetWatcher(PreviewContext ctx)
		{
			_previewerState = ctx.State;
			_previewerRuntime = ctx.Runtime;
			_invalidation = ctx.Invalidation;
			_hub = ctx.Hub;

			_delayedRebind = new DelayedAction(DelayedRebind);
		}

		#endregion Constructors

		#region Methods

		internal void Clear()
		{
			_watchedAssetPaths.Clear();
			_lastSeenVersion = 0;
			_delayedRebind.Cancel();
		}

		internal void BuildWatchedAssetPaths(AnimatorController controller)
		{
			_watchedAssetPaths.Clear();

			TryWatchControllerAndDependencies(controller);
			TryWatchAsset(_previewerState.previewClip);

			ConsumeAssetWatcherVersion();
		}

		internal void TryWatchAsset(UnityEngine.Object asset)
		{
			if (asset == null)
				return;

			string path = AssetDatabase.GetAssetPath(asset);
			if (string.IsNullOrEmpty(path))
				return;

			AddWatchedPath(path);
		}

		internal void ProcessAssetWatcher()
		{
			if (!_previewerState.autoRebindOnAssetChanges || !_previewerRuntime.isBound)
				return;

			bool hasChanges = AnimatorPreviewerAssetWatcher.TryCollectChangesSince(
				_lastSeenVersion,
				out int newVersion,
				out HashSet<string> changedPaths);

			if (!hasChanges)
				return;

			_lastSeenVersion = newVersion;
			
			// Clip mode just needs a simple UI refresh
			if (_previewerState.mode != AnimatorPreviewerMode.Controller)
			{
				_previewerState.clipEventsRevision++;
				_invalidation.Add(PreviewInvalidationFlags.FullUI);
				return;
			}

			if (changedPaths == null || changedPaths.Count == 0)
				return;

			if (ShouldRebindForChangedPaths(changedPaths))
				QueueRebind();
		}

		internal void QueueRebind()
		{
			if (_delayedRebind.IsQueued)
				return;

			_delayedRebind.Queue();
		}

		private void ConsumeAssetWatcherVersion()
		{
			_lastSeenVersion = AnimatorPreviewerAssetWatcher.CurrentVersion;
		}

		private void TryWatchControllerAndDependencies(AnimatorController controller)
		{
			if (controller == null)
				return;

			string controllerPath = AssetDatabase.GetAssetPath(controller);
			if (string.IsNullOrEmpty(controllerPath))
				return;

			AddWatchedPath(controllerPath);
			TryWatchDependencies(controllerPath);
		}

		private void TryWatchDependencies(string assetPath)
		{
			if (string.IsNullOrEmpty(assetPath))
				return;

			try
			{
				string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);
				if (dependencies == null)
					return;

				for (int i = 0; i < dependencies.Length; i++)
					AddWatchedPath(dependencies[i]);
			}
			catch { /* ignored */ }
		}

		private void AddWatchedPath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;

			_watchedAssetPaths.Add(path);
		}

		private bool ShouldRebindForChangedPaths(HashSet<string> changedPaths)
		{
			foreach (string changedPath in changedPaths)
			{
				if (_watchedAssetPaths.Contains(changedPath))
					return true;
			}

			return false;
		}

		private void DelayedRebind()
		{
			if (!_previewerRuntime.isBound)
				return;

			if (EditorBusy.IsBusy())
			{
				_delayedRebind.Queue();
				return;
			}

			try
			{
				_hub.RequestRebind();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}

		#endregion Methods
	}
}

