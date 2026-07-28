using System.Collections.Generic;
using GWOO.Editor.ParticlePreview;
using UnityEngine;
using UnityEngine.Animations;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Handles firing of AnimationEvents during preview playback.
	/// </summary>
	internal sealed class PreviewAnimationEvents
	{
		#region Fields

		private readonly AnimatorPreviewerState _previewerState;
		private readonly AnimatorPreviewerRuntime _previewerRuntime;
		private readonly PreviewFxBridge _fxBridge;

		private readonly Dictionary<ClipKey, ClipTracker> _controllerEventTrackers = new();

		private static readonly List<AnimationEvent> SCRATCH_EVENT_LIST = new(16);
		private static readonly List<float> SCRATCH_NORM_LIST = new(16);

		#endregion Fields

		#region Constructors

		internal PreviewAnimationEvents(PreviewContext ctx)
		{
			_previewerState = ctx.State;
			_previewerRuntime = ctx.Runtime;
			_fxBridge = ctx.FxBridge;
		}

		#endregion Constructors

		#region Methods

		internal void ClearControllerEventTrackers() => _controllerEventTrackers.Clear();
		
		internal void ForceAnimatorFireEventsOff()
		{
			if (!_previewerRuntime.isBound || _previewerRuntime.boundAnimator == null)
				return;

			if (_previewerRuntime.boundAnimator.fireEvents)
				_previewerRuntime.boundAnimator.fireEvents = false;
		}

		internal void FireClipEventsBetweenTimeline(AnimationClip clip, float timelineLen, float fromTimelineSec, float toTimelineSec)
		{
			if (!_previewerState.eventsEnabled || clip == null)
				return;

			if (toTimelineSec <= fromTimelineSec)
				return;

			float fromClipSec = TimelineSecToClipSec(clip, timelineLen, fromTimelineSec);
			float toClipSec = TimelineSecToClipSec(clip, timelineLen, toTimelineSec);

			if (fromTimelineSec <= 0f)
				fromClipSec = -0.0001f;

			AnimationEvent[] clipEvents = ClipEventsUtility.GetClipEventsSafe(clip);
			if (clipEvents == null || clipEvents.Length == 0)
				return;

			GameObject target = _previewerState.targetAnimator != null ? _previewerState.targetAnimator.gameObject : null;

			for (int i = 0; i < clipEvents.Length; i++)
			{
				AnimationEvent animationEvent = clipEvents[i];
				if (animationEvent.time > fromClipSec && animationEvent.time <= toClipSec)
					DispatchAnimationEvent(target, clip, animationEvent);
			}
		}

		internal void FireClipEventsBetweenClipAbs(AnimationClip clip, double fromClipAbs, double toClipAbs)
		{
			if (!_previewerState.eventsEnabled || clip == null)
				return;

			if (toClipAbs <= fromClipAbs)
				return;

			float clipLen = Mathf.Max(1e-6f, clip.length);

			int prevLoop = Mathf.FloorToInt((float)(fromClipAbs / clipLen));
			int curLoop = Mathf.FloorToInt((float)(toClipAbs / clipLen));
			float prevFrac = Mathf.Repeat((float)fromClipAbs, clipLen) / clipLen;
			float curFrac = Mathf.Repeat((float)toClipAbs, clipLen) / clipLen;

			GameObject target = _previewerState.targetAnimator != null ? _previewerState.targetAnimator.gameObject : null;

			FireClipEventsForward(target, clip, prevLoop, prevFrac, curLoop, curFrac);
		}

		private static float TimelineSecToClipSec(AnimationClip clip, float timelineLen, float timelineSec)
		{
			if (clip == null)
				return 0f;

			float tLen = Mathf.Max(1e-6f, timelineLen);
			float cLen = Mathf.Max(1e-6f, clip.length);
			return (timelineSec / tLen) * cLen;
		}

		private void FireClipEventsForward(GameObject target, AnimationClip clip, int prevLoop, float prevFrac, int curLoop, float curFrac)
		{
			AnimationEvent[] clipEvents = ClipEventsUtility.GetClipEventsSafe(clip);
			if (clipEvents == null || clipEvents.Length == 0)
				return;

			SCRATCH_EVENT_LIST.Clear();
			SCRATCH_NORM_LIST.Clear();

			float clipLen = Mathf.Max(clip.length, 1e-6f);

			for (int i = 0; i < clipEvents.Length; i++)
			{
				AnimationEvent animationEvent = clipEvents[i];
				SCRATCH_EVENT_LIST.Add(animationEvent);
				SCRATCH_NORM_LIST.Add(Mathf.Clamp01(animationEvent.time / clipLen));
			}

			if (curLoop == prevLoop)
			{
				for (int i = 0; i < SCRATCH_EVENT_LIST.Count; i++)
				{
					float normalized = SCRATCH_NORM_LIST[i];
					if (normalized > prevFrac && normalized <= curFrac)
						DispatchAnimationEvent(target, clip, SCRATCH_EVENT_LIST[i]);
				}

				return;
			}

			if (curLoop <= prevLoop)
				return;
			
			for (int i = 0; i < SCRATCH_EVENT_LIST.Count; i++)
			{
				float normalized = SCRATCH_NORM_LIST[i];
				if (normalized > prevFrac && normalized <= 1f)
					DispatchAnimationEvent(target, clip, SCRATCH_EVENT_LIST[i]);
			}

			for (int loop = prevLoop + 1; loop < curLoop; loop++)
			{
				for (int i = 0; i < SCRATCH_EVENT_LIST.Count; i++)
					DispatchAnimationEvent(target, clip, SCRATCH_EVENT_LIST[i]);
			}

			for (int i = 0; i < SCRATCH_EVENT_LIST.Count; i++)
			{
				float normalized = SCRATCH_NORM_LIST[i];
				if (normalized >= 0f && normalized <= curFrac)
					DispatchAnimationEvent(target, clip, SCRATCH_EVENT_LIST[i]);
			}
		}

		private void DispatchAnimationEvent(GameObject target, AnimationClip clip, AnimationEvent animationEvent)
		{
			if (target == null)
				return;

			if (string.IsNullOrEmpty(animationEvent.functionName))
				return;

			using (new FxPreviewContext.Scope(animationEvent.time, _fxBridge.LastContextKey, _fxBridge.SessionId))
			{
				if (animationEvent.objectReferenceParameter != null)
				{
					if (_previewerState.logFiredEvents)
						Debug.Log($"[AnimatorPreviewer] {clip.name}: {animationEvent.functionName}(Object)", target);

					target.SendMessage(animationEvent.functionName, animationEvent.objectReferenceParameter, SendMessageOptions.DontRequireReceiver);
					return;
				}

				if (!string.IsNullOrEmpty(animationEvent.stringParameter))
				{
					if (_previewerState.logFiredEvents)
						Debug.Log($"[AnimatorPreviewer] {clip.name}: {animationEvent.functionName}(\"{animationEvent.stringParameter}\")", target);

					target.SendMessage(animationEvent.functionName, animationEvent.stringParameter, SendMessageOptions.DontRequireReceiver);
					return;
				}

				if (!Mathf.Approximately(animationEvent.floatParameter, 0f))
				{
					if (_previewerState.logFiredEvents)
						Debug.Log($"[AnimatorPreviewer] {clip.name}: {animationEvent.functionName}({animationEvent.floatParameter})", target);

					target.SendMessage(animationEvent.functionName, animationEvent.floatParameter, SendMessageOptions.DontRequireReceiver);
					return;
				}

				if (animationEvent.intParameter != 0)
				{
					if (_previewerState.logFiredEvents)
						Debug.Log($"[AnimatorPreviewer] {clip.name}: {animationEvent.functionName}({animationEvent.intParameter})", target);

					target.SendMessage(animationEvent.functionName, animationEvent.intParameter, SendMessageOptions.DontRequireReceiver);
					return;
				}

				if (_previewerState.logFiredEvents)
					Debug.Log($"[AnimatorPreviewer] {clip.name}: {animationEvent.functionName}()", target);

				target.SendMessage(animationEvent.functionName, SendMessageOptions.DontRequireReceiver);
			}
		}

		internal void FireControllerEvents(PreviewBinding binding)
		{
			if (!binding.TryGetControllerPlayable(out AnimatorControllerPlayable boundPlayable))
				return;

			int layerCount = _previewerState.ResolvedTargetController?.layers?.Length ?? 1;

			for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
			{
				AnimatorStateInfo stateInfo;
				try
				{
						stateInfo = boundPlayable.GetCurrentAnimatorStateInfo(layerIndex);
				}
				catch { continue; }

				float normalizedTime = stateInfo.normalizedTime;
				int loopIndex = Mathf.FloorToInt(normalizedTime);
				float loopFraction = normalizedTime - loopIndex;

				AnimatorClipInfo[] clipInfos;
				try { clipInfos = boundPlayable.GetCurrentAnimatorClipInfo(layerIndex); }
				catch { continue; }

				if (clipInfos == null)
					continue;

				GameObject target = _previewerState.targetAnimator != null ? _previewerState.targetAnimator.gameObject : null;

				for (int i = 0; i < clipInfos.Length; i++)
				{
					AnimatorClipInfo clipInfo = clipInfos[i];
					AnimationClip clip = clipInfo.clip;
					if (clip == null)
						continue;

					if (clipInfo.weight < _previewerState.eventClipWeightThreshold)
						continue;

					ClipKey key = new() { layer = layerIndex, clipId = clip.GetInstanceID() };

					if (!_controllerEventTrackers.TryGetValue(key, out ClipTracker tracker))
					{
						tracker.lastLoop = loopIndex;
						tracker.lastFrac = loopFraction;
						_controllerEventTrackers[key] = tracker;

						FireClipEventsForward(target, clip, loopIndex, -0.0001f, loopIndex, loopFraction);
						continue;
					}

					FireClipEventsForward(target, clip, tracker.lastLoop, tracker.lastFrac, loopIndex, loopFraction);

					tracker.lastLoop = loopIndex;
					tracker.lastFrac = loopFraction;
					_controllerEventTrackers[key] = tracker;
				}
			}
		}

		#endregion Methods
	}
}

