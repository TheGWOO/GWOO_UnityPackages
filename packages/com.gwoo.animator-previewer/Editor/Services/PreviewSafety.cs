using System;
using System.Collections.Generic;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Manages safety operations like unbinding, restoring poses, and handling snapshots
	/// to ensure the scene is not left in a broken state.
	/// </summary>
	internal sealed class PreviewSafety
	{
		#region Fields

		private readonly AnimatorPreviewerState _previewerState;
		private readonly AnimatorPreviewerRuntime _previewerRuntime;
		private readonly PreviewBinding _binding;
		private readonly PreviewInvalidation _invalidation;
		private readonly PreviewHub _hub;
		private readonly IClipEditsResolver _clipEditsResolver;

		private readonly PreviewSafetySnapshot _snapshot = new();
		private bool _isBusyRestoring;

		#endregion Fields

		#region Constructors

		internal PreviewSafety(
			PreviewContext ctx,
			PreviewBinding binding,
			IClipEditsResolver clipEditsResolver)
		{
			_previewerState = ctx.State;
			_previewerRuntime = ctx.Runtime;
			_invalidation = ctx.Invalidation;
			_hub = ctx.Hub;
			
			_binding = binding;
			_clipEditsResolver = clipEditsResolver;
		}

		#endregion Constructors

		#region Methods

		internal void Clear()
		{
			_snapshot.Clear();
			_isBusyRestoring = false;
		}

		internal void CaptureSnapshot(IReadOnlyList<Transform> animatedTransforms)
		{
			_snapshot.Capture(animatedTransforms);
		}

		internal void RestorePoseAndPrefabOverrides()
		{
			_snapshot.RestorePoseAndPrefabOverrides();
		}

		internal void SafetyUnbind(string reason, bool clearAnimatorField)
		{
			Debug.Log($"AnimatorPreviewer safety unbind: {reason}");
			
			_clipEditsResolver?.TryResolvePendingClipEdits($"safety unbind: {reason}", PendingEditsResolution.ApplyRevertOnly);

			EditorBusy.Push();
			try
			{
				_binding.Unbind();
			}
			finally
			{
				EditorBusy.Pop();
			}

			if (clearAnimatorField)
				_previewerState.targetAnimator = null;

			_invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		internal void SafetyRestorePoseSnapshot(string reason)
		{
			if (_isBusyRestoring || _snapshot.IsEmpty)
				return;

			Debug.Log($"AnimatorPreviewer safety pose snapshot restored: {reason}");

			EditorBusy.Push();
			_isBusyRestoring = true;

			try
			{
				_snapshot.RestorePoseAndPrefabOverrides();
				_binding.RestoreAnimatorSettings(clearCache: false);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				CancelQueuedPoseRestoring();
				return;
			}

			// Keep busy state until SafetyRestorePreview is called.
			_invalidation.Add(PreviewInvalidationFlags.Scene);
		}

		internal void SafetyRestorePreview(string reason)
		{
			if (!_isBusyRestoring)
				return;

			Debug.Log($"AnimatorPreviewer safety preview restored: {reason}");

			CancelQueuedPoseRestoring();

			try
			{
				if (!_previewerRuntime.graph.IsValid())
					return;

				_binding.ApplyAnimatorPreviewSettingsNoCache();

				_hub.RequestGraphEvaluation(0f);
				_binding.LockRootIfNeeded();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
			finally
			{
				_invalidation.Add(PreviewInvalidationFlags.Scene | PreviewInvalidationFlags.Header);
			}
		}

		private void CancelQueuedPoseRestoring()
		{
			if (!_isBusyRestoring)
				return;

			_isBusyRestoring = false;
			EditorBusy.Pop();
		}

		#endregion Methods
	}
}

