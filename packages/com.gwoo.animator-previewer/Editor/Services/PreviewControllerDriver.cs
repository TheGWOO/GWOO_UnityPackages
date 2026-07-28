using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GWOO.Editor.Tools
{
	internal class PreviewControllerDriver
	{
		#region Fields

		private const double UI_UPDATE_INTERVAL = 0.033; // 30 FPS

		private readonly AnimatorPreviewerState _previewerState;
		private readonly AnimatorPreviewerRuntime _previewerRuntime;
		
		private readonly PreviewInvalidation _invalidation;
		private readonly PreviewBinding _binding;
		private readonly PreviewAnimationEvents _animationEvents;
		private readonly PreviewHub _hub;

		private double _lastUiUpdateTime;

		#endregion Fields

		#region Properties

		public AnimatorController ActiveController => _previewerState.ResolvedTargetController;

		#endregion Properties

		#region Constructors

		internal PreviewControllerDriver(
			PreviewContext ctx,
			PreviewBinding binding,
			PreviewAnimationEvents animationEvents)
		{
			_previewerState = ctx.State;
			_previewerRuntime = ctx.Runtime;
			_hub = ctx.Hub;
			_invalidation = ctx.Invalidation;
			
			_binding = binding;
			_animationEvents = animationEvents;
		}

		#endregion Constructors

		#region Methods

		internal void Update(float dt)
		{
			if (!_binding.TryGetControllerPlayable(out AnimatorControllerPlayable boundPlayable))
			{
				_binding.EnsureBindingIsValid();
				return;
			}
			
			float scaledDt = dt * Mathf.Max(0f, _previewerState.timeScale);

			if (_previewerRuntime.isPlaying)
			{
				boundPlayable.SetSpeed(1.0);
				_hub.RequestGraphEvaluation(scaledDt);

				if (_previewerState.eventsEnabled)
					_animationEvents.FireControllerEvents(_binding);

				PreviewTimelineDriver.Advance(scaledDt);

				// Always invalidate the scene for smooth playback
				_invalidation.Add(PreviewInvalidationFlags.Scene);

				// Throttle UI updates to avoid lag
				double now = EditorApplication.timeSinceStartup;
				if (now - _lastUiUpdateTime >= UI_UPDATE_INTERVAL)
				{
					_lastUiUpdateTime = now;
					_invalidation.Add(PreviewInvalidationFlags.ControllerContext | PreviewInvalidationFlags.RightPanelParams | PreviewInvalidationFlags.Header);
				}
			}
			else
			{
				boundPlayable.SetSpeed(0.0);
				_hub.RequestGraphEvaluation(0f);
			}

			_binding.LockRootIfNeeded();
		}
		
		internal bool TryGetLayerWeight(int layerIndex, out float weight)
		{
			weight = 0f;
			if (!_previewerRuntime.acPlayable.IsValid()) return false;

			try { weight = _previewerRuntime.acPlayable.GetLayerWeight(layerIndex); return true; }
			catch { return false; }
		}

		internal void SetLayerWeight(int layerIndex, float weight)
		{
			if (!_previewerRuntime.acPlayable.IsValid()) return;

			try
			{
				_previewerRuntime.acPlayable.SetLayerWeight(layerIndex, weight);
				_hub.RequestGraphEvaluation(0f);
				_invalidation.Add(PreviewInvalidationFlags.RightPanelParams | PreviewInvalidationFlags.ControllerContext | PreviewInvalidationFlags.Scene);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				_hub.RequestRebind();
			}
		}

		internal bool TryGetBool(int id, out bool value)
		{
			value = default;
			if (!_previewerRuntime.acPlayable.IsValid()) return false;
			value = _previewerRuntime.acPlayable.GetBool(id);
			return true;
		}

		internal bool TryGetInt(int id, out int value)
		{
			value = default;
			if (!_previewerRuntime.acPlayable.IsValid()) return false;
			value = _previewerRuntime.acPlayable.GetInteger(id);
			return true;
		}

		internal bool TryGetFloat(int id, out float value)
		{
			value = default;
			if (!_previewerRuntime.acPlayable.IsValid()) return false;
			value = _previewerRuntime.acPlayable.GetFloat(id);
			return true;
		}

		internal void SetFloat(int hash, float v)
		{
			if (!_previewerRuntime.acPlayable.IsValid()) return;

			try
			{
				_previewerRuntime.acPlayable.SetFloat(hash, v);
				_hub.RequestGraphEvaluation(0f);
				_invalidation.Add(PreviewInvalidationFlags.RightPanelParams | PreviewInvalidationFlags.Scene);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				_hub.RequestRebind();
			}
		}

		internal void SetInt(int hash, int v)
		{
			if (!_previewerRuntime.acPlayable.IsValid()) return;

			try
			{
				_previewerRuntime.acPlayable.SetInteger(hash, v);
				_hub.RequestGraphEvaluation(0f);
				_invalidation.Add(PreviewInvalidationFlags.RightPanelParams | PreviewInvalidationFlags.Scene);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				_hub.RequestRebind();
			}
		}

		internal void SetBool(int hash, bool v)
		{
			if (!_previewerRuntime.acPlayable.IsValid()) return;

			try
			{
				_previewerRuntime.acPlayable.SetBool(hash, v);
				_hub.RequestGraphEvaluation(0f);
				_invalidation.Add(PreviewInvalidationFlags.RightPanelParams | PreviewInvalidationFlags.Scene);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				_hub.RequestRebind();
			}
		}

		internal void SetTrigger(int hash)
		{
			if (!_previewerRuntime.acPlayable.IsValid()) return;

			try
			{
				_previewerRuntime.acPlayable.SetTrigger(hash);
				_hub.RequestGraphEvaluation(0f);
				_invalidation.Add(PreviewInvalidationFlags.RightPanelParams | PreviewInvalidationFlags.Scene);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				_hub.RequestRebind();
			}
		}

		internal void ResetTrigger(int hash)
		{
			if (!_previewerRuntime.acPlayable.IsValid()) return;

			try
			{
				_previewerRuntime.acPlayable.ResetTrigger(hash);
				_hub.RequestGraphEvaluation(0f);
				_invalidation.Add(PreviewInvalidationFlags.RightPanelParams | PreviewInvalidationFlags.Scene);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				_hub.RequestRebind();
			}
		}

		// ---------------- Controller context ----------------
		internal void QueryControllerContext(List<AnimatorPreviewerControllerLayerContext> buffer)
		{
			buffer.Clear();

			if (!_previewerRuntime.acPlayable.IsValid())
				return;

			AnimatorController controller = _previewerState.ResolvedTargetController;
			int layerCount = controller?.layers?.Length ?? 0;
			if (layerCount <= 0)
				return;

			for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
			{
				bool inTransition = false;

				AnimatorStateInfo currentState = default;
				AnimatorStateInfo nextState = default;
				AnimatorTransitionInfo transitionInfo = default;

				try
				{
					inTransition = _previewerRuntime.acPlayable.IsInTransition(layerIndex);
					currentState = _previewerRuntime.acPlayable.GetCurrentAnimatorStateInfo(layerIndex);

					if (inTransition)
					{
						nextState = _previewerRuntime.acPlayable.GetNextAnimatorStateInfo(layerIndex);
						transitionInfo = _previewerRuntime.acPlayable.GetAnimatorTransitionInfo(layerIndex);
					}
				}
				catch
				{
					// Keep panel stable.
				}

				AnimatorClipInfo[] clipInfos = null;
				try { clipInfos = _previewerRuntime.acPlayable.GetCurrentAnimatorClipInfo(layerIndex); }
				catch { /* ignore */ }

				AnimatorPreviewerControllerClipInfo[] clips = Array.Empty<AnimatorPreviewerControllerClipInfo>();
				if (clipInfos != null && clipInfos.Length > 0)
				{
					clips = new AnimatorPreviewerControllerClipInfo[clipInfos.Length];

					for (int clipIndex = 0; clipIndex < clipInfos.Length; clipIndex++)
					{
						AnimationClip clip = clipInfos[clipIndex].clip;

						string clipName = clip != null ? clip.name : "(null)";
						int clipId = clip != null ? clip.GetInstanceID() : 0;

						clips[clipIndex] = new AnimatorPreviewerControllerClipInfo(
							clipId,
							clipName,
							clipInfos[clipIndex].weight);
					}
				}

				string layerName = string.Empty;
				if (controller != null && controller.layers != null && layerIndex >= 0 && layerIndex < controller.layers.Length)
					layerName = controller.layers[layerIndex].name;

				buffer.Add(new AnimatorPreviewerControllerLayerContext(
					layerIndex: layerIndex,
					layerName: layerName,
					currentStateHash: currentState.fullPathHash,
					currentNormalized: currentState.normalizedTime,
					inTransition: inTransition,
					nextStateHash: inTransition ? nextState.fullPathHash : 0,
					transitionNormalized: inTransition ? transitionInfo.normalizedTime : 0f,
					clips: clips));
			}
		}

		// ---------------- State play / clip preview ----------------
		internal void PlayStateController(int layerIndex, int stateHash)
		{
			if (!_previewerRuntime.acPlayable.IsValid())
				return;

			_hub.RequestPlaybackStop();

			_previewerState.mode = AnimatorPreviewerMode.Controller;
			_hub.RequestPlayableSync();

			_previewerRuntime.acPlayable.PlayInFixedTime(stateHash, layerIndex, 0f);
			_previewerRuntime.acPlayable.SetLayerWeight(layerIndex, 1f);

			_previewerRuntime.isPlaying = true;
			_previewerRuntime.acPlayable.SetSpeed(1.0);

			_invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.ControllerContext | PreviewInvalidationFlags.RightPanelParams | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		internal void PreviewContextClip(int clipInstanceId, float normalizedTime, PreviewClipTimeline clipTimeline)
		{
			if (!_previewerRuntime.acPlayable.IsValid() || clipTimeline == null)
				return;

			AnimationClip found = FindCurrentClipByInstanceId(clipInstanceId);
			if (found == null)
			{
				Debug.LogWarning($"AnimatorPreviewer: Could not resolve clip instance id {clipInstanceId} from current controller context.");
				return;
			}

			_hub.RequestPlaybackStop();

			_previewerState.mode = AnimatorPreviewerMode.Clip;
			clipTimeline.BuildClipPlayable(found, preserveTimelineTime: false);

			normalizedTime = Mathf.Repeat(normalizedTime, 1f);
			_previewerRuntime.timelineTime = Mathf.Clamp(normalizedTime * _previewerRuntime.timelineLength, 0f, _previewerRuntime.timelineLength);

			clipTimeline.ClampLoopRangeToTimeline();
			_previewerRuntime.timelineTime = Mathf.Clamp((float)_previewerRuntime.timelineTime, 0f, _previewerRuntime.timelineLength);
			clipTimeline.SampleClipAtTimelineTime(_previewerRuntime.timelineTime);

			_invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		private AnimationClip FindCurrentClipByInstanceId(int clipInstanceId)
		{
			if (clipInstanceId == 0)
				return null;

			AnimatorController controller = _previewerState.ResolvedTargetController;
			int layerCount = controller?.layers?.Length ?? 0;
			if (layerCount <= 0) layerCount = 1;

			for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
			{
				AnimatorClipInfo[] infos = null;
				try { infos = _previewerRuntime.acPlayable.GetCurrentAnimatorClipInfo(layerIndex); }
				catch { /* ignore */ }

				if (infos == null)
					continue;

				for (int i = 0; i < infos.Length; i++)
				{
					AnimationClip clip = infos[i].clip;
					if (clip != null && clip.GetInstanceID() == clipInstanceId)
						return clip;
				}
			}

			return null;
		}

		#endregion Methods
	}
}

