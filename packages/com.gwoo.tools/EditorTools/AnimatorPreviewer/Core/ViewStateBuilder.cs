namespace GWOO.Editor.Tools
{
	internal static class ViewStateBuilder
	{
		internal static AnimatorPreviewerViewState Build(AnimatorPreviewerState previewerState, AnimatorPreviewerRuntime previewerRuntime, PreviewAnimationStates animationStates)
		{
			return new AnimatorPreviewerViewState(
				isBound: previewerRuntime.isBound,
				isPlaying: previewerRuntime.isPlaying,
				mode: previewerState.mode,
				targetAnimator: previewerState.targetAnimator,
				controllerOverride: previewerState.controllerOverride,
				autoBindToSelection: previewerState.autoBindToSelection,
				lockRootPosition: previewerState.lockRootPosition,
				lockRootRotation: previewerState.lockRootRotation,
				timeScale: previewerState.timeScale,
				previewClip: previewerState.previewClip,
				fps: previewerState.fps,
				loop: previewerState.loop,
				useClipLength: previewerState.useClipLength,
				snapLengthToFps: previewerState.snapLengthToFps,
				customTimelineLength: previewerState.customTimelineLength,
				timelineLength: previewerRuntime.timelineLength,
				timelineTime: previewerRuntime.timelineTime,
				loopRangeEnabled: previewerState.loopRangeEnabled,
				loopRangeStartSec: previewerState.loopRangeStartSec,
				loopRangeEndSec: previewerState.loopRangeEndSec,
				eventsEnabled: previewerState.eventsEnabled,
				logFiredEvents: previewerState.logFiredEvents,
				drawEventMarkers: previewerState.drawEventMarkers,
				eventClipWeightThreshold: previewerState.eventClipWeightThreshold,
				clipEventsRevision: previewerState.clipEventsRevision,
				autoRebindOnAssetChanges: previewerState.autoRebindOnAssetChanges,
				stateSearch: previewerState.stateSearch,
				stateDisplayFlags: previewerState.stateDisplayFlags,
				selectedStateLayer: previewerState.selectedStateLayer,
				selectedStateHash: previewerState.selectedStateHash,
				totalStateCount: animationStates.TotalCount,
				visibleStateCount: animationStates.VisibleCount);
		}
	}
}
