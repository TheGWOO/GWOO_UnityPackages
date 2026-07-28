using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Host API used by UI panels to query state and dispatch user commands.
	/// </summary>
	public interface IAnimatorPreviewerHost
	{
		AnimatorPreviewerTheme Theme { get; }

		AnimatorPreviewerViewState GetViewState();

		// Header
		void CmdSetTargetAnimator(Animator animator);
		void CmdToggleBind();
		void CmdFocusSceneView();
		bool CmdSetMode(AnimatorPreviewerMode mode);
		void CmdSetAutoBind(bool enabled);
		void CmdSetLockPos(bool enabled);
		void CmdSetLockRot(bool enabled);

		// Clip panel
		bool CmdSetPreviewClip(AnimationClip clip);
		void CmdSetFps(int fps);
		void CmdSetUseClipLength(bool enabled);
		void CmdSetSnapLengthToFps(bool enabled);
		void CmdSetCustomTimelineLength(float seconds);

		void CmdTimelineScrubStart(float timeSec);
		void CmdTimelineScrubMove(float timeSec);
		void CmdTimelineScrubEnd(float timeSec);

		void CmdTimelineLoopRangeChanged(float a, float b);
		void CmdTimelineClearLoopRange();

		void CmdStepFrames(int frames);
		void CmdResetTimeline();

		// Playback
		void CmdTogglePlayPause();
		void CmdSetTimeScale(float value);
		void CmdSetLoop(bool value);

		// Controller panel
		void CmdSetControllerOverride(AnimatorController controller);
		void CmdSetAutoRebindOnAssetChanges(bool enabled);

		// Events settings (shared)
		void CmdSetEventsEnabled(bool enabled);
		void CmdSetLogFiredEvents(bool enabled);
		void CmdSetDrawEventMarkers(bool enabled);
		void CmdSetEventClipWeightThreshold(float value);

		// Clip events IO (Edit Events)
		AnimationEvent[] GetClipEventsSafe(AnimationClip clip);
		bool TryApplyClipEvents(AnimationClip clip, AnimationEvent[] eventsToWrite, string undoLabel, out AnimationClip refreshedClip);
		AnimationClip TryRefreshClipReference(AnimationClip clip);

		// States panel
		IReadOnlyList<AnimatorPreviewerStateEntry> GetVisibleStates();
		void CmdSetStateSearch(string query);
		void CmdSetStateDisplayFlag(AnimatorPreviewerStateDisplayFlags flag, bool enabled);
		void CmdRefreshStates();
		void CmdSetSelectedState(int layerIndex, int stateHash);
		void CmdPreviewSelectedStateClip(Motion motion);
		void CmdPlaySelectedStateController(int layerIndex, int stateHash);

		// Right panels (layers / params)
		AnimatorController GetActiveController();

		bool TryGetLayerWeight(int layerIndex, out float weight);
		void CmdSetLayerWeight(int layerIndex, float weight);

		bool TryGetFloat(int hash, out float v);
		bool TryGetInt(int hash, out int v);
		bool TryGetBool(int hash, out bool v);

		void CmdSetFloat(int hash, float v);
		void CmdSetInt(int hash, int v);
		void CmdSetBool(int hash, bool v);

		void CmdSetTrigger(int hash);
		void CmdResetTrigger(int hash);

		// Controller context (for Controller panel)
		void CmdPreviewContextClip(int clipInstanceId, float normalizedTime);
		void QueryControllerContext(List<AnimatorPreviewerControllerLayerContext> buffer);

		string ResolveStateName(int layer, int fullPathHash);
	}
}

