using UnityEditor.Animations;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	public readonly struct AnimatorPreviewerViewState
	{
		public readonly bool isBound;
		public readonly bool isPlaying;
		public readonly AnimatorPreviewerMode mode;

		public readonly Animator targetAnimator;
		public readonly AnimatorController controllerOverride;

		public readonly bool autoBindToSelection;
		public readonly bool lockRootPosition;
		public readonly bool lockRootRotation;

		public readonly float timeScale;

		// Clip/timeline
		public readonly AnimationClip previewClip;
		public readonly int fps;
		public readonly bool loop;

		public readonly bool useClipLength;
		public readonly bool snapLengthToFps;
		public readonly float customTimelineLength;

		public readonly float timelineLength;
		public readonly double timelineTime;

		public readonly bool loopRangeEnabled;
		public readonly float loopRangeStartSec;
		public readonly float loopRangeEndSec;

		// Events
		public readonly bool eventsEnabled;
		public readonly bool logFiredEvents;
		public readonly bool drawEventMarkers;
		public readonly float eventClipWeightThreshold;
		public readonly int clipEventsRevision;

		// Auto rebind
		public readonly bool autoRebindOnAssetChanges;

		// States
		public readonly string stateSearch;
		public readonly AnimatorPreviewerStateDisplayFlags stateDisplayFlags;
		public readonly int selectedStateLayer;
		public readonly int selectedStateHash;
		public readonly int totalStateCount;
		public readonly int visibleStateCount;

		public AnimatorPreviewerViewState(
			bool isBound, bool isPlaying, AnimatorPreviewerMode mode,
			Animator targetAnimator, AnimatorController controllerOverride,
			bool autoBindToSelection, bool lockRootPosition, bool lockRootRotation,
			float timeScale,
			AnimationClip previewClip, int fps, bool loop,
			bool useClipLength, bool snapLengthToFps, float customTimelineLength,
			float timelineLength, double timelineTime,
			bool loopRangeEnabled, float loopRangeStartSec, float loopRangeEndSec,
			bool eventsEnabled, bool logFiredEvents, bool drawEventMarkers,
			float eventClipWeightThreshold, int clipEventsRevision,
			bool autoRebindOnAssetChanges,
			string stateSearch, AnimatorPreviewerStateDisplayFlags stateDisplayFlags,
			int selectedStateLayer, int selectedStateHash,
			int totalStateCount, int visibleStateCount)
		{
			this.isBound = isBound;
			this.isPlaying = isPlaying;
			this.mode = mode;

			this.targetAnimator = targetAnimator;
			this.controllerOverride = controllerOverride;

			this.autoBindToSelection = autoBindToSelection;
			this.lockRootPosition = lockRootPosition;
			this.lockRootRotation = lockRootRotation;

			this.timeScale = timeScale;

			this.previewClip = previewClip;
			this.fps = fps;
			this.loop = loop;

			this.useClipLength = useClipLength;
			this.snapLengthToFps = snapLengthToFps;
			this.customTimelineLength = customTimelineLength;

			this.timelineLength = timelineLength;
			this.timelineTime = timelineTime;

			this.loopRangeEnabled = loopRangeEnabled;
			this.loopRangeStartSec = loopRangeStartSec;
			this.loopRangeEndSec = loopRangeEndSec;

			this.eventsEnabled = eventsEnabled;
			this.logFiredEvents = logFiredEvents;
			this.drawEventMarkers = drawEventMarkers;
			this.eventClipWeightThreshold = eventClipWeightThreshold;
			this.clipEventsRevision = clipEventsRevision;

			this.autoRebindOnAssetChanges = autoRebindOnAssetChanges;

			this.stateSearch = stateSearch;
			this.stateDisplayFlags = stateDisplayFlags;
			this.selectedStateLayer = selectedStateLayer;
			this.selectedStateHash = selectedStateHash;
			this.totalStateCount = totalStateCount;
			this.visibleStateCount = visibleStateCount;
		}
	}
}
