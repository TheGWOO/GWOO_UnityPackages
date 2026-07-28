using System;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Core component responsible for managing animation preview functionality, including binding, timeline control,
	/// state management, and safety features. Acts as the central orchestrator for the animation preview system.
	/// </summary>
	internal sealed class PreviewCore
	{
		#region Fields

		private readonly AnimatorPreviewerState _previewerState;

		private readonly PreviewContext _ctx;

		private readonly PreviewBinding _binding;
		private readonly PreviewAssetWatcher _assetWatcher;
		private readonly PreviewTransformResolver _transformResolver;
		private readonly PreviewAnimationEvents _animationEvents;
		private readonly PreviewAnimationStates _animationStates;
		private readonly PreviewClipTimeline _clipTimeline;
		private readonly PreviewControllerDriver _controllerDriver;
		private readonly PreviewSafety _safety;
		private readonly PreviewUpdateLoop _update;

		private bool _rebindRequested;

		#endregion Fields

		#region Properties

		internal PreviewClipTimeline ClipTimeline => _clipTimeline;
		internal PreviewControllerDriver ControllerDriver => _controllerDriver;
		internal PreviewAnimationStates AnimationStates => _animationStates;

		internal bool IsBound => _binding.IsBound;

		internal PreviewInvalidationFlags ConsumeInvalidation() => _ctx.Invalidation.Consume();

		#endregion Properties

		#region Constructor

		internal PreviewCore(AnimatorPreviewerState previewerState, IClipEditsResolver clipEditsResolver)
		{
			_previewerState = previewerState;

			AnimatorPreviewerRuntime previewerRuntime = new();
			PreviewHub hub = new();
			PreviewInvalidation invalidation = new();
			PreviewFxBridge fxBridge = new(_previewerState);

			_ctx = new PreviewContext(_previewerState, previewerRuntime, invalidation, fxBridge, hub);

			_binding = new PreviewBinding(_ctx);

			_assetWatcher = new PreviewAssetWatcher(_ctx);
			_transformResolver = new PreviewTransformResolver(_ctx);

			_animationEvents = new PreviewAnimationEvents(_ctx);
			_animationStates = new PreviewAnimationStates(_ctx);

			_controllerDriver = new PreviewControllerDriver(_ctx, _binding, _animationEvents);
			_clipTimeline = new PreviewClipTimeline(_ctx, _binding, _animationEvents, _assetWatcher);

			_safety = new PreviewSafety(_ctx, _binding, clipEditsResolver);
			_update = new PreviewUpdateLoop(_ctx, _binding, _clipTimeline, _controllerDriver, _animationEvents, _assetWatcher);

			SubscribeToHubEvents();
		}

		private void SubscribeToHubEvents()
		{
			_ctx.Hub.RebindRequested += () => _rebindRequested = true;
			_ctx.Hub.SafetyUnbindRequested += (reason, clearAnimatorField) => _safety.SafetyUnbind(reason, clearAnimatorField);
			_ctx.Hub.RestorePoseRequested += () => _safety.RestorePoseAndPrefabOverrides();
			_ctx.Hub.PlaybackStopRequested += StopPlayback;
			_ctx.Hub.PlayableSyncRequested += SyncOutputPlayableForCurrentMode;
			_ctx.Hub.GraphEvaluationRequested += dt => TryEvaluateGraph(dt);

			_ctx.Hub.Bound += OnBound;
			_ctx.Hub.Unbound += OnUnbound;
		}

		#endregion Constructor

		#region Orchestrators

		internal AnimatorPreviewerViewState BuildViewState() => ViewStateBuilder.Build(_previewerState, _ctx.Runtime, _animationStates);

		internal void QueueBind() => _binding.QueueBind();
		internal void Bind() => _binding.Bind();
		internal void Unbind() => _binding.Unbind();
		internal void Rebind() => _binding.Rebind();
		
		internal PreviewInvalidationFlags Tick()
		{
			if (_rebindRequested)
			{
				_rebindRequested = false;
				_binding.Rebind();
			}

			_update.Tick();
			return _ctx.Invalidation.Consume();
		}

		internal void SetTargetAnimator(Animator animator)
		{
			if (_previewerState.targetAnimator == animator)
				return;

			_previewerState.targetAnimator = animator;

			if (_ctx.Runtime.isBound)
			{
				_binding.Unbind();
			}

			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Header);
		}

		internal void StopPlayback()
		{
			_ctx.Runtime.isPlaying = false;

			if (_ctx.Runtime.acPlayable.IsValid())
				_ctx.Runtime.acPlayable.SetSpeed(0.0);

			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header);
		}

		internal bool TryApplyClipEvents(AnimationClip clip, AnimationEvent[] eventsToWrite, string undoLabel, out AnimationClip refreshedClip)
		{
			bool ok = ClipEventsUtility.TryApplyClipEvents(clip, eventsToWrite, undoLabel, out refreshedClip);

			if (!_ctx.Runtime.isBound)
				return ok;

			_ctx.FxBridge.BumpContext();
			_ctx.FxBridge.SyncContext(force: true);

			return ok;
		}

		internal bool SetMode(AnimatorPreviewerMode mode)
		{
			if (_previewerState.mode == mode)
				return false;

			_previewerState.mode = mode;
			_ctx.Runtime.isScrubbing = false;
			StopPlayback();

			if (_ctx.Runtime.isBound)
			{
				_ctx.FxBridge.BumpContext();
				_ctx.FxBridge.SyncContext(force: true);

				SyncOutputPlayableForCurrentMode();

				if (mode == AnimatorPreviewerMode.Controller)
				{
					_animationEvents.ClearControllerEventTrackers();
					TryEvaluateGraph(0f);
					_binding.LockRootIfNeeded();

					_ctx.Invalidation.Add(
						PreviewInvalidationFlags.ControllerContext
						| PreviewInvalidationFlags.RightPanelParams
						| PreviewInvalidationFlags.Header
						| PreviewInvalidationFlags.Scene);
				}
				else
				{
					if (_previewerState.previewClip != null)
					{
						_clipTimeline.BuildClipPlayable(_previewerState.previewClip, preserveTimelineTime: false);
					}
					else if (_clipTimeline.TryGetCurrentDominantClip(out AnimationClip dominant))
					{
						_clipTimeline.BuildClipPlayable(dominant, preserveTimelineTime: false);
					}
					else
					{
						_clipTimeline.UpdateEffectiveTimelineLength();
						_clipTimeline.ClampLoopRangeToTimeline();
						_ctx.Invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Header);
					}
				}
			}

			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI);
			return true;
		}

		internal void SetPreviewClip(AnimationClip clip)
		{
			if (_previewerState.previewClip == clip && !_ctx.Runtime.isBound)
			{
				_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Header);
				return;
			}

			_previewerState.previewClip = clip;

			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Header);

			if (!_ctx.Runtime.isBound)
				return;

			StopPlayback();

			if (clip != null)
				_previewerState.mode = AnimatorPreviewerMode.Clip;

			if (_previewerState.mode == AnimatorPreviewerMode.Clip)
			{
				if (clip != null)
				{
					_clipTimeline.BuildClipPlayable(clip, preserveTimelineTime: false);

					_ctx.Runtime.timelineTime = 0.0;
					_clipTimeline.ClampLoopRangeToTimeline();

					_ctx.Runtime.timelineTime = Mathf.Clamp((float)_ctx.Runtime.timelineTime, 0f, _ctx.Runtime.timelineLength);
					_clipTimeline.SampleClipAtTimelineTime(_ctx.Runtime.timelineTime);

					_ctx.Invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Scene);
				}
				else if (_clipTimeline.TryGetCurrentDominantClip(out AnimationClip dominant))
				{
					_clipTimeline.BuildClipPlayable(dominant, preserveTimelineTime: false);
					_ctx.Invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Scene);
				}
				else
				{
					_clipTimeline.UpdateEffectiveTimelineLength();
					_clipTimeline.ClampLoopRangeToTimeline();
					_ctx.Invalidation.Add(PreviewInvalidationFlags.Timeline);
				}
			}

			SyncOutputPlayableForCurrentMode();
		}

		internal void RecomputeTimeline()
		{
			_clipTimeline.UpdateEffectiveTimelineLength();
			_clipTimeline.ClampLoopRangeToTimeline();
			_ctx.Invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Header);
		}

		internal void SetFps(int fps)
		{
			_previewerState.fps = Mathf.Clamp(fps, 1, 240);
			RecomputeTimeline();
		}

		internal void ApplyStateFilter()
		{
			_animationStates.ApplyStateFilter();
			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI);
		}

		internal void RefreshStates()
		{
			_animationStates.RefreshStates();
			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI);
		}

		internal bool TryPreviewSelectedStateClip(Motion motion, out string error)
		{
			error = string.Empty;

			if (!_binding.IsBound)
			{
				error = "Not bound.";
				return false;
			}

			if (!MotionLeafClipUtility.TryGetFirstLeafClip(motion, out AnimationClip clip) || clip == null)
			{
				error = "This state has no direct clip to preview.";
				return false;
			}
			
			AnimationClip effectiveClip = _animationStates.ResolveOverriddenClip(clip);
			
			SetPreviewClip(effectiveClip);
			return true;
		}


		internal void SafetyUnbind(string reason, bool clearAnimatorField) => _safety.SafetyUnbind(reason, clearAnimatorField);
		internal void SafetyRestorePoseSnapshot(string reason) => _safety.SafetyRestorePoseSnapshot(reason);
		internal void SafetyRestorePreview(string reason) => _safety.SafetyRestorePreview(reason);

		#endregion Orchestrators

		#region State setters (no Consume here)

		internal void SetAutoBindToSelection(bool enabled)
		{
			if (_previewerState.autoBindToSelection == enabled)
				return;

			_previewerState.autoBindToSelection = enabled;
			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header);
		}

		internal void SetLockRootPosition(bool enabled)
		{
			if (_previewerState.lockRootPosition == enabled)
				return;

			_previewerState.lockRootPosition = enabled;

			if (_ctx.Runtime.isBound && enabled && _ctx.Runtime.root != null)
				_ctx.Runtime.rootPos = _ctx.Runtime.root.position;

			_binding.LockRootIfNeeded();

			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		internal void SetLockRootRotation(bool enabled)
		{
			if (_previewerState.lockRootRotation == enabled)
				return;

			_previewerState.lockRootRotation = enabled;

			if (_ctx.Runtime.isBound && enabled && _ctx.Runtime.root != null)
				_ctx.Runtime.rootRot = _ctx.Runtime.root.rotation;

			_binding.LockRootIfNeeded();

			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		internal void SetUseClipLength(bool enabled)
		{
			if (_previewerState.useClipLength == enabled)
				return;

			_previewerState.useClipLength = enabled;
			RecomputeTimeline();
		}

		internal void SetSnapLengthToFps(bool enabled)
		{
			if (_previewerState.snapLengthToFps == enabled)
				return;

			_previewerState.snapLengthToFps = enabled;
			RecomputeTimeline();
		}

		internal void SetCustomTimelineLength(float seconds)
		{
			float clamped = Mathf.Max(0.033f, seconds);
			if (Mathf.Approximately(_previewerState.customTimelineLength, clamped))
				return;

			_previewerState.customTimelineLength = clamped;
			RecomputeTimeline();
		}

		internal void SetTimeScale(float value)
		{
			float clamped = Mathf.Clamp(value, 0f, 2f);
			if (Mathf.Approximately(_previewerState.timeScale, clamped))
				return;

			_previewerState.timeScale = clamped;
			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Playback);
		}

		internal void SetLoop(bool value)
		{
			if (_previewerState.loop == value)
				return;

			_previewerState.loop = value;
			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Playback);
		}

		internal void SetEventsEnabled(bool enabled)
		{
			if (_previewerState.eventsEnabled == enabled)
				return;

			_previewerState.eventsEnabled = enabled;

			// controller mode uses trackers, clear when toggled for safety
			_animationEvents.ClearControllerEventTrackers();

			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header);
		}

		internal void SetLogFiredEvents(bool enabled)
		{
			if (_previewerState.logFiredEvents == enabled)
				return;

			_previewerState.logFiredEvents = enabled;
			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header);
		}

		internal void SetDrawEventMarkers(bool enabled)
		{
			if (_previewerState.drawEventMarkers == enabled)
				return;

			_previewerState.drawEventMarkers = enabled;
			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header | PreviewInvalidationFlags.FullUI);
		}

		internal void SetEventClipWeightThreshold(float value)
		{
			float clamped = Mathf.Clamp01(value);
			if (Mathf.Approximately(_previewerState.eventClipWeightThreshold, clamped))
				return;

			_previewerState.eventClipWeightThreshold = clamped;
			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header);
		}

		internal void SetStateSearch(string query)
		{
			string q = query ?? string.Empty;
			if (_previewerState.stateSearch == q)
				return;

			_previewerState.stateSearch = q;
			ApplyStateFilter();
		}

		internal void SetStateDisplayFlag(AnimatorPreviewerStateDisplayFlags flag, bool enabled)
		{
			AnimatorPreviewerStateDisplayFlags before = _previewerState.stateDisplayFlags;

			if (enabled) _previewerState.stateDisplayFlags |= flag;
			else _previewerState.stateDisplayFlags &= ~flag;

			if (_previewerState.stateDisplayFlags == before)
				return;

			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI);
		}

		internal void SetSelectedState(int layerIndex, int stateHash)
		{
			if (_previewerState.selectedStateLayer == layerIndex && _previewerState.selectedStateHash == stateHash)
				return;

			_previewerState.selectedStateLayer = layerIndex;
			_previewerState.selectedStateHash = stateHash;

			_ctx.Invalidation.Add(PreviewInvalidationFlags.RightPanelStates);
		}

		internal void SetControllerOverride(AnimatorController controller)
		{
			if (_previewerState.controllerOverride == controller)
				return;

			_previewerState.controllerOverride = controller;

			if (_ctx.Runtime.isBound)
			{
				_binding.Rebind();
				return;
			}

			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Header);
		}

		internal void SetAutoRebindOnAssetChanges(bool enabled)
		{
			if (_previewerState.autoRebindOnAssetChanges == enabled)
				return;

			_previewerState.autoRebindOnAssetChanges = enabled;
			_ctx.Invalidation.Add(PreviewInvalidationFlags.Header);
		}

		#endregion State setters

		#region Hub signals

		private void TryEvaluateGraph(float dt)
		{
			if (!_ctx.Runtime.graph.IsValid() || EditorBusy.IsBusy())
				return;

			if (dt < 0f) dt = 0f;

			try
			{
				_ctx.Runtime.graph.Evaluate(dt);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				_binding.Rebind();
			}
		}

		private void SyncOutputPlayableForCurrentMode()
		{
			switch (_previewerState.mode)
			{
				case AnimatorPreviewerMode.Controller:
					if (_ctx.Runtime.acPlayable.IsValid())
						_ctx.Runtime.output.SetSourcePlayable(_ctx.Runtime.acPlayable);
					return;

				case AnimatorPreviewerMode.Clip:
					if (_ctx.Runtime.clipBuilt && _ctx.Runtime.clipPlayable.IsValid())
						_ctx.Runtime.output.SetSourcePlayable(_ctx.Runtime.clipPlayable);
					return;
			}
		}

		private void OnBound(AnimatorController controller)
		{
			_transformResolver.RebuildAnimatedTransformSet(controller);
			_safety.CaptureSnapshot(_ctx.Runtime.animatedTransforms);

			_animationStates.RefreshStates();
			_assetWatcher.BuildWatchedAssetPaths(controller);

			if (_previewerState.mode == AnimatorPreviewerMode.Clip)
			{
				if (_previewerState.previewClip != null)
				{
					_clipTimeline.BuildClipPlayable(_previewerState.previewClip, preserveTimelineTime: false);
				}
				else if (_clipTimeline.TryGetCurrentDominantClip(out AnimationClip dominant))
				{
					_clipTimeline.BuildClipPlayable(dominant, preserveTimelineTime: false);
				}
				else
				{
					_clipTimeline.UpdateEffectiveTimelineLength();
					_clipTimeline.ClampLoopRangeToTimeline();
					_ctx.Invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Header);
				}
			}
			else
			{
				_animationEvents.ClearControllerEventTrackers();
				TryEvaluateGraph(0f);
				_binding.LockRootIfNeeded();
			}

			SyncOutputPlayableForCurrentMode();

			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		private void OnUnbound()
		{
			_assetWatcher.Clear();
			_transformResolver.Clear();
			_animationEvents.ClearControllerEventTrackers();
			_animationStates.RefreshStates();
			_safety.Clear();

			_ctx.Invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		#endregion Hub signals
	}
}

