using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Manages the timeline playback logic for AnimationClips, including scrubbing, looping, and time synchronization.
	/// </summary>
	internal sealed class PreviewClipTimeline
	{
		#region Fields

		private const double UI_UPDATE_INTERVAL = 0.033; // 30 FPS

		private readonly AnimatorPreviewerState _previewerState;
		private readonly AnimatorPreviewerRuntime _previewerRuntime;
		private readonly PreviewBinding _binding;
		private readonly PreviewAnimationEvents _animationEvents;
		private readonly PreviewFxBridge _fxBridge;
		private readonly PreviewAssetWatcher _assetWatcher;
		private readonly PreviewInvalidation _invalidation;
		private readonly PreviewHub _hub;

		private double _lastUiUpdateTime;

		#endregion Fields

		#region Properties

		private bool HasLoopRange
		{
			get
			{
				float minLen = 1f / Mathf.Max(1, _previewerState.fps);
				return _previewerState.loopRangeEnabled && _previewerState.loopRangeEndSec > _previewerState.loopRangeStartSec + minLen;
			}
		}

		private float PlayRegionStart => HasLoopRange ? _previewerState.loopRangeStartSec : 0f;
		private float PlayRegionEnd => HasLoopRange ? _previewerState.loopRangeEndSec : _previewerRuntime.timelineLength;

		#endregion Properties

		#region Constructors

		internal PreviewClipTimeline(
			PreviewContext ctx,
			PreviewBinding binding,
			PreviewAnimationEvents animationEvents,
			PreviewAssetWatcher assetWatcher)
		{
			_previewerState = ctx.State;
			_previewerRuntime = ctx.Runtime;
			_binding = binding;
			_animationEvents = animationEvents;
			_fxBridge = ctx.FxBridge;
			_assetWatcher = assetWatcher;
			_invalidation = ctx.Invalidation;
			_hub = ctx.Hub;
		}

		#endregion Constructors

		#region Methods

		internal void Update(float dt)
		{
			UpdateEffectiveTimelineLength();
			ClampLoopRangeToTimeline();

			bool clipWasBuilt = _previewerRuntime.clipBuilt;

			EnsureClipPlayableBuiltIfNeeded();

			if (!clipWasBuilt && _previewerRuntime.clipBuilt)
				_invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Scene);

			if (_previewerRuntime.clipBuilt && _previewerState.previewClip != null)
			{
				if (_previewerRuntime.isPlaying && !_previewerRuntime.isScrubbing)
				{
					AdvanceAndSample(dt);

					_invalidation.Add(PreviewInvalidationFlags.Scene);

					double now = EditorApplication.timeSinceStartup;
					if (now - _lastUiUpdateTime >= UI_UPDATE_INTERVAL)
					{
						_lastUiUpdateTime = now;
						_invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Header);
					}
				}

				_binding.LockRootIfNeeded();
			}
		}

		private void EnsureClipPlayableBuiltIfNeeded()
		{
			if (_previewerRuntime.clipBuilt)
				return;

			if (_previewerState.previewClip != null)
			{
				BuildClipPlayable(_previewerState.previewClip, preserveTimelineTime: true);
				return;
			}

			if (TryGetCurrentDominantClip(out AnimationClip dominantClip))
				BuildClipPlayable(dominantClip, preserveTimelineTime: true);
		}

		private void AdvanceAndSample(float dt)
		{
			float start = _previewerState.loopRangeEnabled ? _previewerState.loopRangeStartSec : 0f;
			float end = _previewerState.loopRangeEnabled ? _previewerState.loopRangeEndSec : _previewerRuntime.timelineLength;

			if (end <= start + 1e-6f)
			{
				start = 0f;
				end = _previewerRuntime.timelineLength;
			}

			float from = Mathf.Clamp((float)_previewerRuntime.timelineTime, start, end);

			float advance = dt * Mathf.Max(0f, _previewerState.timeScale);
			float to = from + advance;

			if (_previewerState.loop)
			{
				int safety = 0;
				while (to > end && safety++ < 64)
				{
					if (_previewerState.eventsEnabled && _previewerState.previewClip != null)
						_animationEvents.FireClipEventsBetweenTimeline(_previewerState.previewClip, _previewerRuntime.timelineLength, from, end);

					to = start + (to - end);
					from = start;
				}

				if (_previewerState.eventsEnabled && _previewerState.previewClip != null && to > from)
					_animationEvents.FireClipEventsBetweenTimeline(_previewerState.previewClip, _previewerRuntime.timelineLength, from, to);

				_previewerRuntime.timelineTime = Mathf.Clamp(to, start, end);
			}
			else
			{
				float clampedTo = Mathf.Clamp(to, start, end);

				if (_previewerState.eventsEnabled && _previewerState.previewClip != null && clampedTo > from)
					_animationEvents.FireClipEventsBetweenTimeline(_previewerState.previewClip, _previewerRuntime.timelineLength, from, clampedTo);

				_previewerRuntime.timelineTime = clampedTo;
			}

			SampleClipAtTimelineTime(_previewerRuntime.timelineTime);
		}

		internal void UpdateEffectiveTimelineLength()
		{
			float desiredLength;

			if (_previewerState.mode == AnimatorPreviewerMode.Clip && _previewerState.useClipLength && _previewerState.previewClip != null)
				desiredLength = Mathf.Max(0.033f, _previewerState.previewClip.length);
			else
				desiredLength = Mathf.Max(0.033f, _previewerState.customTimelineLength);

			if (_previewerState.snapLengthToFps)
				desiredLength = Mathf.Ceil(desiredLength * _previewerState.fps) / _previewerState.fps;

			_previewerRuntime.timelineLength = desiredLength;
		}

		internal void ClampLoopRangeToTimeline()
		{
			if (!_previewerState.loopRangeEnabled)
			{
				_previewerRuntime.timelineTime = Mathf.Clamp((float)_previewerRuntime.timelineTime, 0f, _previewerRuntime.timelineLength);
				return;
			}

			_previewerState.loopRangeStartSec = Mathf.Clamp(_previewerState.loopRangeStartSec, 0f, _previewerRuntime.timelineLength);
			_previewerState.loopRangeEndSec = Mathf.Clamp(_previewerState.loopRangeEndSec, 0f, _previewerRuntime.timelineLength);

			float minLen = 1f / Mathf.Max(1, _previewerState.fps);
			if (_previewerState.loopRangeEndSec <= _previewerState.loopRangeStartSec + minLen)
			{
				_previewerState.loopRangeEnabled = false;
				_previewerState.loopRangeStartSec = 0f;
				_previewerState.loopRangeEndSec = 0f;
			}

			_previewerRuntime.timelineTime = ClampToPlayRegion((float)_previewerRuntime.timelineTime);
		}

		private float ClampToPlayRegion(float t)
		{
			float start = Mathf.Clamp(PlayRegionStart, 0f, _previewerRuntime.timelineLength);
			float end = Mathf.Clamp(PlayRegionEnd, 0f, _previewerRuntime.timelineLength);

			if (end < start)
				(start, end) = (end, start);

			return Mathf.Clamp(t, start, end);
		}

		private float WrapInPlayRegion(float t)
		{
			float start = Mathf.Clamp(PlayRegionStart, 0f, _previewerRuntime.timelineLength);
			float end = Mathf.Clamp(PlayRegionEnd, 0f, _previewerRuntime.timelineLength);

			if (end < start)
				(start, end) = (end, start);

			float length = Mathf.Max(1e-6f, end - start);
			return start + Mathf.Repeat(t - start, length);
		}

		private float QuantizeToFrame(float t)
		{
			float frameDuration = 1f / Mathf.Max(1, _previewerState.fps);
			int frameIndex = Mathf.RoundToInt(t / frameDuration);
			return Mathf.Clamp(frameIndex * frameDuration, 0f, _previewerRuntime.timelineLength);
		}

		internal void TogglePlayPause()
		{
			if (!_binding.IsBound)
				return;

			bool wasPlaying = _previewerRuntime.isPlaying;
			_previewerRuntime.isPlaying = !_previewerRuntime.isPlaying;
			_previewerRuntime.lastEvalTime = UnityEditor.EditorApplication.timeSinceStartup;

			if (!wasPlaying && _previewerRuntime.isPlaying && _previewerState.mode == AnimatorPreviewerMode.Clip && HasLoopRange)
			{
				_previewerRuntime.timelineTime = ClampToPlayRegion((float)_previewerRuntime.timelineTime);
				SampleClipAtTimelineTime(_previewerRuntime.timelineTime);
			}

			_invalidation.Add(PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Playback);
		}

		internal void ScrubStart(float wrappedSec)
		{
			if (_previewerState.mode != AnimatorPreviewerMode.Clip || !_binding.IsBound)
				return;

			_previewerRuntime.isScrubbing = true;
			_previewerRuntime.scrubLastTime = _previewerRuntime.timelineTime;

			wrappedSec = ClampToPlayRegion(wrappedSec);
			SetTimelineWrapped(wrappedSec);
		}

		internal void ScrubMove(float wrappedSec)
		{
			if (!_previewerRuntime.isScrubbing || _previewerState.mode != AnimatorPreviewerMode.Clip || !_binding.IsBound)
				return;

			double fromTime = _previewerRuntime.scrubLastTime;

			wrappedSec = ClampToPlayRegion(wrappedSec);
			double toTime = wrappedSec;
			_previewerRuntime.scrubLastTime = toTime;

			if (_previewerState.eventsEnabled && _previewerState.previewClip != null && toTime > fromTime)
			{
				_animationEvents.FireClipEventsBetweenClipAbs(
					_previewerState.previewClip,
					TimelineTimeToClipAbs(fromTime),
					TimelineTimeToClipAbs(toTime));
			}

			SetTimelineWrapped((float)toTime);
		}

		internal void ScrubEnd(float wrappedSec)
		{
			if (!_previewerRuntime.isScrubbing)
				return;

			if (_previewerState.mode != AnimatorPreviewerMode.Clip || !_binding.IsBound)
			{
				_previewerRuntime.isScrubbing = false;
				return;
			}

			wrappedSec = ClampToPlayRegion(wrappedSec);
			SetTimelineWrapped(wrappedSec);

			_previewerRuntime.isScrubbing = false;
		}

		private void SetTimelineWrapped(float wrappedTime)
		{
			wrappedTime = ClampToPlayRegion(wrappedTime);
			_previewerRuntime.timelineTime = wrappedTime;

			SampleClipAtTimelineTime(_previewerRuntime.timelineTime);

			_invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Scene);
		}

		internal void OnTimelineLoopRangeChanged(float a, float b)
		{
			float start = Mathf.Clamp(Mathf.Min(a, b), 0f, _previewerRuntime.timelineLength);
			float end = Mathf.Clamp(Mathf.Max(a, b), 0f, _previewerRuntime.timelineLength);

			float minLen = 1f / Mathf.Max(1, _previewerState.fps);
			if (end - start < minLen)
			{
				ClearLoopRange();
				return;
			}

			_previewerState.loopRangeEnabled = true;
			_previewerState.loopRangeStartSec = start;
			_previewerState.loopRangeEndSec = end;

			_previewerRuntime.timelineTime = ClampToPlayRegion((float)_previewerRuntime.timelineTime);
			SampleClipAtTimelineTime(_previewerRuntime.timelineTime);

			_invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		internal void ClearLoopRange()
		{
			_previewerState.loopRangeEnabled = false;
			_previewerState.loopRangeStartSec = 0f;
			_previewerState.loopRangeEndSec = 0f;

			_previewerRuntime.timelineTime = Mathf.Clamp((float)_previewerRuntime.timelineTime, 0f, _previewerRuntime.timelineLength);

			if (_binding.IsBound && _previewerState.mode == AnimatorPreviewerMode.Clip)
				SampleClipAtTimelineTime(_previewerRuntime.timelineTime);

			_invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}

		internal void StepFrames(int frames)
		{
			if (!_binding.IsBound || _previewerState.mode != AnimatorPreviewerMode.Clip)
				return;

			float from = ClampToPlayRegion((float)_previewerRuntime.timelineTime);

			double frameDuration = 1.0 / Math.Max(1, _previewerState.fps);
			float delta = (float)(frames * frameDuration);

			float to = from + delta;

			if (_previewerState.loop) to = WrapInPlayRegion(to);
			else to = ClampToPlayRegion(to);

			to = QuantizeToFrame(to);
			to = ClampToPlayRegion(to);

			if (_previewerState.eventsEnabled && frames > 0 && _previewerState.previewClip != null && to > from)
				_animationEvents.FireClipEventsBetweenTimeline(_previewerState.previewClip, _previewerRuntime.timelineLength, from, to);

			_previewerRuntime.timelineTime = to;
			SampleClipAtTimelineTime(_previewerRuntime.timelineTime);

			_invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Scene);
		}

		internal void ResetTimeline()
		{
			if (_previewerState.mode != AnimatorPreviewerMode.Clip)
				return;

			_previewerRuntime.timelineTime = HasLoopRange ? _previewerState.loopRangeStartSec : 0.0f;
			SampleClipAtTimelineTime(_previewerRuntime.timelineTime);

			_invalidation.Add(PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Scene);
		}

		private double TimelineTimeToClipAbs(double timelineTime)
		{
			if (_previewerState.previewClip == null)
				return 0.0;

			double clipLen = Math.Max(1e-6, _previewerState.previewClip.length);
			double timelineLen = Math.Max(1e-6, _previewerRuntime.timelineLength);

			return (timelineTime / timelineLen) * clipLen;
		}

		internal void SampleClipAtTimelineTime(double timelineTime)
		{
			if (!_previewerRuntime.clipBuilt || _previewerState.previewClip == null)
				return;

			float timelineSec = ClampToPlayRegion((float)timelineTime);

			float clipLocalTime;
			if (_previewerRuntime.timelineLength > 1e-6f)
				clipLocalTime = (timelineSec / _previewerRuntime.timelineLength) * Mathf.Max(1e-6f, _previewerState.previewClip.length);
			else
				clipLocalTime = 0f;

			clipLocalTime = Mathf.Clamp(clipLocalTime, 0f, Mathf.Max(0f, _previewerState.previewClip.length));

			_previewerRuntime.clipPlayable.SetSpeed(0.0);
			_previewerRuntime.clipPlayable.SetTime(clipLocalTime);
			
			_hub.RequestGraphEvaluation(0f);

			float clipAbs = (float)TimelineTimeToClipAbs(timelineTime);
			PreviewTimelineDriver.SeekAbsolute(clipAbs);
		}

		internal void BuildClipPlayable(AnimationClip clip, bool preserveTimelineTime)
		{
			if (!_previewerRuntime.graph.IsValid())
				return;

			double previousTimelineTime = _previewerRuntime.timelineTime;
			_previewerState.previewClip = PreviewBinding.ResolveClipStable(clip);

			_fxBridge.BumpContext();
			_fxBridge.SyncContext(force: true);

			if (_previewerRuntime.clipBuilt && _previewerRuntime.clipPlayable.IsValid())
			{
				try { _previewerRuntime.clipPlayable.Destroy(); }
				catch { /* ignore */ }

				_previewerRuntime.clipBuilt = false;
			}

			_previewerRuntime.clipPlayable = UnityEngine.Animations.AnimationClipPlayable.Create(_previewerRuntime.graph, _previewerState.previewClip);
			_previewerRuntime.clipPlayable.SetApplyFootIK(false);
			_previewerRuntime.clipPlayable.SetSpeed(0.0);
			_previewerRuntime.clipPlayable.SetTime(0.0);

			_previewerRuntime.output.SetSourcePlayable(_previewerRuntime.clipPlayable);
			_previewerRuntime.clipBuilt = true;

			UpdateEffectiveTimelineLength();
			ClampLoopRangeToTimeline();

			_previewerRuntime.timelineTime = preserveTimelineTime ? previousTimelineTime : 0.0;
			_previewerRuntime.timelineTime = ClampToPlayRegion((float)_previewerRuntime.timelineTime);

			SampleClipAtTimelineTime(_previewerRuntime.timelineTime);

			_assetWatcher.TryWatchAsset(_previewerState.previewClip);

			_hub.RequestGraphEvaluation(0f);

			_invalidation.Add(PreviewInvalidationFlags.FullUI | PreviewInvalidationFlags.Timeline | PreviewInvalidationFlags.Header | PreviewInvalidationFlags.Scene);
		}
		
		internal bool TryGetCurrentDominantClip(out AnimationClip clip)
		{
			clip = null;

			try
			{
				AnimatorClipInfo[] clipInfos;

				if (_previewerRuntime.acPlayable.IsValid())
					clipInfos = _previewerRuntime.acPlayable.GetCurrentAnimatorClipInfo(0);
				else if (_previewerRuntime.boundAnimator != null)
					clipInfos = _previewerRuntime.boundAnimator.GetCurrentAnimatorClipInfo(0);
				else
					clipInfos = null;

				if (clipInfos == null || clipInfos.Length == 0)
					return false;

				float bestWeight = -1f;
				AnimationClip bestClip = null;

				for (int i = 0; i < clipInfos.Length; i++)
				{
					AnimationClip candidate = clipInfos[i].clip;
					if (candidate == null)
						continue;

					if (clipInfos[i].weight > bestWeight)
					{
						bestWeight = clipInfos[i].weight;
						bestClip = candidate;
					}
				}

				clip = bestClip;
				return clip != null;
			}
			catch
			{
				return false;
			}
		}

		#endregion Methods
	}
}


