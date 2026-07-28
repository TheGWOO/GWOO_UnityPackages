using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Manages the binding lifecycle between the Previewer and the target Animator.
	/// Handles Graph creation/destruction, AnimationMode toggling, and state restoration.
	/// </summary>
	internal sealed class PreviewBinding
	{
		#region Fields

		private const string PREVIEW_GRAPH_NAME = "SceneAnimatorPreview";

		private readonly AnimatorPreviewerState _previewerState;
		private readonly AnimatorPreviewerRuntime _previewerRuntime;
		
		private readonly PreviewFxBridge _fxBridge;
		private readonly PreviewInvalidation _invalidation;
		private readonly PreviewHub _hub;

		private readonly DelayedAction _delayedBind;
		private readonly DelayedAction _delayedStopAnimationMode;

		private bool _bindQueued;

		#endregion Fields

		#region Properties

		internal bool IsBound => _previewerRuntime.isBound && _previewerRuntime.boundAnimator != null;
		internal bool HasValidControllerPlayable => _previewerRuntime.acPlayable.IsValid() && IsBound;
		internal bool HasValidPlayableGraph => _previewerRuntime.graph.IsValid() && IsBound;

		#endregion Properties

		#region Constructors

		internal PreviewBinding(PreviewContext ctx)
		{
			_previewerState = ctx.State;
			_previewerRuntime = ctx.Runtime;
			_fxBridge = ctx.FxBridge;
			_invalidation = ctx.Invalidation;
			_hub = ctx.Hub;

			_delayedBind = new DelayedAction(DelayedBind);
			_delayedStopAnimationMode = new DelayedAction(DelayedStopAnimationMode);
		}

		#endregion Constructors

		#region Methods

		internal bool TryGetControllerPlayable(out AnimatorControllerPlayable playable)
		{
			playable = default;
			if (!HasValidControllerPlayable) return false;
			playable = _previewerRuntime.acPlayable;
			return true;
		}
		
		#region Bind & Unbind
		
		internal void QueueBind()
		{
			_bindQueued = true;
			_delayedBind.Queue();
		}

		private void DelayedBind()
		{
			if (!_bindQueued)
				return;

			if (EditorBusy.IsBusy())
			{
				_delayedBind.Queue();
				return;
			}

			_bindQueued = false;

			try { Bind(); }
			catch (Exception exception) { Debug.LogException(exception); }
		}

		internal void Bind()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return;

			if (EditorBusy.IsBusy())
			{
				QueueBind();
				return;
			}

			_delayedBind.Cancel();

			if (_previewerRuntime.isBound)
				return;

			if (_previewerState.targetAnimator == null)
				return;

			_previewerState.previewClip = ResolveClipStable(_previewerState.previewClip);

			AnimatorController controller = _previewerState.ResolvedTargetController;
			if (controller == null)
			{
				Debug.LogWarning("AnimatorPreviewer: No AnimatorController found (neither override nor on Animator).");
				return;
			}
			
			_previewerRuntime.boundAnimator = _previewerState.targetAnimator;

			BeginAnimationModeIfNeeded();
			CacheRootTransform();
			CacheAndApplyAnimatorPreviewSettings();
			CreateAndPlayGraph(_previewerState.ResolvedRuntimeController);

			_previewerRuntime.isBound = true;

			BeginFxSessionAndSyncContext();
			_hub.RequestPlaybackStop();
			ResetPreviewState();

			_hub.RequestPlayableSync();

			_invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);

			_hub.RaiseBound(controller);
		}

		internal void Unbind()
		{
			_bindQueued = false;
			_delayedBind.Cancel();
			_delayedStopAnimationMode.Cancel();

			if (!_previewerRuntime.isBound)
				return;

			_fxBridge.EndSession();

			_hub.RequestPlaybackStop();
			
			DestroyGraphSafe();
			StopAnimationModeIfStarted();
			
			_hub.RequestRestorePose();

			RestoreInitialRootTransform();
			RestoreAnimatorSettings(clearCache: true);
			
			_previewerRuntime.Terminate();
			
			_hub.RaiseUnbound();
		}

		internal void Rebind()
		{
			Unbind();
			QueueBind();
		}

		internal void EnsureBindingIsValid()
		{
			if (!_previewerRuntime.isBound)
				return;

			if (_previewerRuntime.boundAnimator == null)
			{
				_hub.RequestSafetyUnbind("bound animator reference lost", clearAnimatorField: true);
				return;
			}

			if (_previewerState.targetAnimator != null)
			{
				if (_previewerRuntime.graph.IsValid() && _previewerRuntime.acPlayable.IsValid())
					return;

				_hub.RequestRebind();
				return;
			}

			_hub.RequestSafetyUnbind("animator reference lost", clearAnimatorField: true);
		}
		
		#endregion Bind & Unbind
		
		#region Root & Animator settings
		
		private void CacheRootTransform()
		{
			_previewerRuntime.root = _previewerRuntime.boundAnimator.transform;

			_previewerRuntime.initialRootPos = _previewerRuntime.root.position;
			_previewerRuntime.initialRootRot = _previewerRuntime.root.rotation;

			_previewerRuntime.rootPos = _previewerRuntime.initialRootPos;
			_previewerRuntime.rootRot = _previewerRuntime.initialRootRot;
		}

		private void CacheAndApplyAnimatorPreviewSettings()
		{
			_previewerRuntime.oldCullingMode = _previewerRuntime.boundAnimator.cullingMode;
			_previewerRuntime.oldApplyRootMotion = _previewerRuntime.boundAnimator.applyRootMotion;
			_previewerRuntime.oldFireEvents = _previewerRuntime.boundAnimator.fireEvents;
			_previewerRuntime.hasSavedAnimatorSettings = true;

			ApplyAnimatorPreviewSettingsNoCache();
		}

		internal void ApplyAnimatorPreviewSettingsNoCache()
		{
			Animator currentAnimator = _previewerRuntime.boundAnimator;
			if (currentAnimator == null)
				return;

			currentAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			currentAnimator.applyRootMotion = false;
			currentAnimator.fireEvents = false;
		}

		internal void RestoreAnimatorSettings(bool clearCache)
		{
			Animator currentAnimator = _previewerRuntime.boundAnimator;
			if (!_previewerRuntime.hasSavedAnimatorSettings || currentAnimator == null)
				return;

			currentAnimator.cullingMode = _previewerRuntime.oldCullingMode;
			currentAnimator.applyRootMotion = _previewerRuntime.oldApplyRootMotion;
			currentAnimator.fireEvents = _previewerRuntime.oldFireEvents;

			if (clearCache)
				_previewerRuntime.hasSavedAnimatorSettings = false;
		}

		private void RestoreInitialRootTransform()
		{
			if (_previewerRuntime.root == null)
				return;

			_previewerRuntime.root.position = _previewerRuntime.initialRootPos;
			_previewerRuntime.root.rotation = _previewerRuntime.initialRootRot;
		}

		internal void LockRootIfNeeded()
		{
			if (_previewerRuntime.root == null)
				return;

			if (_previewerState.lockRootPosition)
				_previewerRuntime.root.position = _previewerRuntime.rootPos;

			if (_previewerState.lockRootRotation)
				_previewerRuntime.root.rotation = _previewerRuntime.rootRot;
		}
		
		#endregion Root & Animator settings
		
		#region Graph

		private void CreateAndPlayGraph(RuntimeAnimatorController controller)
		{
			_previewerRuntime.graph = PlayableGraph.Create(PREVIEW_GRAPH_NAME);
			_previewerRuntime.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

			_previewerRuntime.acPlayable = AnimatorControllerPlayable.Create(_previewerRuntime.graph, controller);

			_previewerRuntime.output = AnimationPlayableOutput.Create(_previewerRuntime.graph, "Animation", _previewerRuntime.boundAnimator);
			_previewerRuntime.output.SetSourcePlayable(_previewerRuntime.acPlayable);

			_previewerRuntime.graph.Play();
		}

		private void DestroyGraphSafe()
		{
			try
			{
				if (_previewerRuntime.graph.IsValid())
					_previewerRuntime.graph.Destroy();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
			finally
			{
				_previewerRuntime.graph = default;
				_previewerRuntime.output = default;
				_previewerRuntime.acPlayable = default;
				_previewerRuntime.clipPlayable = default;
				_previewerRuntime.clipBuilt = false;
			}
		}

		private void BeginFxSessionAndSyncContext()
		{
			_fxBridge.BeginSessionIfNeeded();
			_fxBridge.BumpContext();
			_fxBridge.SyncContext(force: true);
		}

		private void ResetPreviewState()
		{
			_previewerRuntime.timelineTime = 0.0;
			_previewerRuntime.lastEvalTime = EditorApplication.timeSinceStartup;
		}

		internal static AnimationClip ResolveClipStable(AnimationClip clip)
		{
			if (clip == null)
				return null;

			string path = AssetDatabase.GetAssetPath(clip);
			if (string.IsNullOrEmpty(path))
				return clip;

			UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(path);
			if (main is AnimationClip)
				return clip;

			try
			{
				UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
				if (subAssets == null || subAssets.Length == 0)
					return clip;

				string targetName = clip.name;
				for (int i = 0; i < subAssets.Length; i++)
				{
					if (subAssets[i] is AnimationClip subClip && subClip != null && subClip.name == targetName)
						return subClip;
				}
			}
			catch { /* ignore */ }

			return clip;
		}

		#endregion Graph
		
		#region Animation mode
		
		private void BeginAnimationModeIfNeeded()
		{
			if (!AnimationMode.InAnimationMode())
			{
				AnimationMode.StartAnimationMode();
				_previewerRuntime.startedAnimationMode = true;
				return;
			}

			_previewerRuntime.startedAnimationMode = false;
		}

		private void StopAnimationModeIfStarted()
		{
			if (!_previewerRuntime.startedAnimationMode)
				return;

			_delayedStopAnimationMode.Cancel();

			try
			{
				if (AnimationMode.InAnimationMode())
					AnimationMode.StopAnimationMode();

				_previewerRuntime.startedAnimationMode = false;
			}
			catch
			{
				_previewerRuntime.startedAnimationMode = true;
				QueueStopAnimationMode();
			}
		}

		private void QueueStopAnimationMode() => _delayedStopAnimationMode.Queue();

		private void DelayedStopAnimationMode()
		{
			if (EditorBusy.IsBusy())
			{
				_delayedStopAnimationMode.Queue();
				return;
			}

			try
			{
				if (_previewerRuntime.startedAnimationMode && AnimationMode.InAnimationMode())
					AnimationMode.StopAnimationMode();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
			finally
			{
				_previewerRuntime.startedAnimationMode = false;
			}
		}
		
		#endregion Animation mode

		#endregion Methods
	}
}

