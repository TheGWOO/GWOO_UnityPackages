using System;
using UnityEditor.Animations;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	[Serializable]
	internal sealed class AnimatorPreviewerState
	{
		public Animator targetAnimator;
		public AnimatorController controllerOverride;
		public bool autoBindToSelection = true;

		public bool lockRootPosition = true;
		public bool lockRootRotation = true;

		public AnimatorPreviewerMode mode = AnimatorPreviewerMode.Clip;

		public float timeScale = 1f;

		public AnimationClip previewClip;
		public int fps = 60;
		public bool loop = true;

		public bool useClipLength = true;
		public bool snapLengthToFps = true;
		public float customTimelineLength = 2.0f;

		public bool loopRangeEnabled;
		public float loopRangeStartSec;
		public float loopRangeEndSec;

		public bool eventsEnabled = true;
		public bool logFiredEvents;
		public bool drawEventMarkers = true;
		[Range(0f, 1f)] public float eventClipWeightThreshold = 0.2f;

		public int clipEventsRevision;

		public bool autoRebindOnAssetChanges = true;

		public string stateSearch = string.Empty;
		public AnimatorPreviewerStateDisplayFlags stateDisplayFlags = AnimatorPreviewerStateDisplayFlags.None;

		public int selectedStateLayer = -1;
		public int selectedStateHash;

		internal AnimatorController ResolvedTargetController
		{
			get
			{
				if (controllerOverride != null)
					return controllerOverride;

				RuntimeAnimatorController runtimeController = targetAnimator != null ? targetAnimator.runtimeAnimatorController : null;
				return UnwrapToAnimatorController(runtimeController);
			}
		}

		internal RuntimeAnimatorController ResolvedRuntimeController
		{
			get
			{
				if (controllerOverride != null)
					return controllerOverride;

				return targetAnimator != null ? targetAnimator.runtimeAnimatorController : null;
			}
		}

		internal bool TryGetResolvedOverrideController(out AnimatorOverrideController controller)
		{
			if (controllerOverride != null)
			{
				controller = null;
				return false;
			}

			controller = targetAnimator != null
				? targetAnimator.runtimeAnimatorController as AnimatorOverrideController
				: null;

			return controller != null;
		}

		private static AnimatorController UnwrapToAnimatorController(RuntimeAnimatorController runtimeController)
		{
			while (runtimeController is AnimatorOverrideController overrideController)
				runtimeController = overrideController.runtimeAnimatorController;

			return runtimeController as AnimatorController;
		}
	}
}
