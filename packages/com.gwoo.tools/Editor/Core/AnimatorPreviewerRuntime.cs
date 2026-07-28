using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GWOO.Editor.Tools
{
	internal sealed class AnimatorPreviewerRuntime
	{
		// Playables
		public PlayableGraph graph;
		public AnimationPlayableOutput output;
		public AnimatorControllerPlayable acPlayable;

		public AnimationClipPlayable clipPlayable;
		public bool clipBuilt;

		// Session
		public bool isBound;
		public bool startedAnimationMode;
		public bool isPlaying;
		public Animator boundAnimator;

		// Root lock/cache
		public Transform root;
		public Vector3 rootPos;
		public Quaternion rootRot;

		public Vector3 initialRootPos;
		public Quaternion initialRootRot;

		// Animator settings cache
		public AnimatorCullingMode oldCullingMode;
		public bool oldApplyRootMotion;
		public bool oldFireEvents;
		public bool hasSavedAnimatorSettings;

		// Timeline
		public float timelineLength = 2.0f;
		public double timelineTime;

		public bool isScrubbing;
		public double scrubLastTime;

		// Update clocks
		public double lastEvalTime;
		public double lastExceptionLogTime;

		// Animated transform sets
		public readonly List<Transform> animatedTransforms = new(256);
		public readonly HashSet<int> animatedTransformIds = new(256);
		public readonly HashSet<int> driverTransformIds = new(256);

		public void Terminate()
		{
			isBound = false;
			isPlaying = false;

			clipBuilt = false;

			animatedTransforms.Clear();
			animatedTransformIds.Clear();
			driverTransformIds.Clear();

			root = null;
			boundAnimator = null;
		}
	}
}
