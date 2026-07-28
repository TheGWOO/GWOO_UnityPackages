using System;
using UnityEditor.Animations;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Tiny event hub for cross-cutting signals/requests to avoid service cycles.
	/// </summary>
	internal sealed class PreviewHub
	{
		public event Action RebindRequested;
		public event Action<string, bool> SafetyUnbindRequested;
		public event Action RestorePoseRequested;
		public event Action PlaybackStopRequested;
		public event Action PlayableSyncRequested;
		public event Action<float> GraphEvaluationRequested;

		public event Action<AnimatorController> Bound;
		public event Action Unbound;

		public void RequestRebind() => RebindRequested?.Invoke();
		public void RequestSafetyUnbind(string reason, bool clearAnimatorField) => SafetyUnbindRequested?.Invoke(reason, clearAnimatorField);
		public void RequestRestorePose() => RestorePoseRequested?.Invoke();
		public void RequestPlaybackStop() => PlaybackStopRequested?.Invoke();
		public void RequestPlayableSync() => PlayableSyncRequested?.Invoke();
		public void RequestGraphEvaluation(float dt) => GraphEvaluationRequested?.Invoke(dt);

		public void RaiseBound(AnimatorController controller) => Bound?.Invoke(controller);
		public void RaiseUnbound() => Unbound?.Invoke();
	}
}

