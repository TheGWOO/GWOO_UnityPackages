using System;
using UnityEditor;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Main update loop driver. Delegates to specific mode drivers (ClipTimeline or ControllerDriver) based on state.
	/// Handles exception throttling and safe delta time calculation.
	/// </summary>
	internal sealed class PreviewUpdateLoop
	{
		#region Fields

		private const float MAX_EDITOR_DELTA_TIME = 1f / 15f;
		private const double EXCEPTION_LOG_COOLDOWN_SEC = 1.0;

		private readonly AnimatorPreviewerState _previewerState;
		private readonly AnimatorPreviewerRuntime _previewerRuntime;

		private readonly PreviewBinding _binding;
		private readonly PreviewClipTimeline _clipTimeline;
		private readonly PreviewControllerDriver _controllerDriver;
		private readonly PreviewAnimationEvents _animationEvents;
		private readonly PreviewAssetWatcher _assetWatcher;
		private readonly PreviewFxBridge _fxBridge;

		#endregion Fields

		#region Constructors

		internal PreviewUpdateLoop(
			PreviewContext ctx,
			PreviewBinding binding,
			PreviewClipTimeline clipTimeline,
			PreviewControllerDriver controllerDriver,
			PreviewAnimationEvents animationEvents,
			PreviewAssetWatcher assetWatcher)
		{
			_previewerState = ctx.State;
			_previewerRuntime = ctx.Runtime;
			_fxBridge = ctx.FxBridge;

			_binding = binding;
			_clipTimeline = clipTimeline;
			_controllerDriver = controllerDriver;
			_animationEvents = animationEvents;
			_assetWatcher = assetWatcher;
		}

		#endregion Constructors

		#region Methods

		internal void Tick()
		{
			if (!_binding.HasValidPlayableGraph)
			{
				_binding.EnsureBindingIsValid();
				return;
			}

			_animationEvents.ForceAnimatorFireEventsOff();

			if (EditorBusy.IsBusy())
				return;

			_assetWatcher.ProcessAssetWatcher();

			float dt = ComputeClampedDeltaTime();

			_fxBridge.SyncContext(force: false);

			try
			{
				if (_previewerState.mode == AnimatorPreviewerMode.Clip)
					_clipTimeline.Update(dt);
				else
					_controllerDriver.Update(dt);
			}
			catch (Exception exception)
			{
				LogExceptionThrottled(exception);
				_assetWatcher.QueueRebind();
			}
		}

		private float ComputeClampedDeltaTime()
		{
			double now = EditorApplication.timeSinceStartup;
			if (now < 0.0)
			{
				_previewerRuntime.lastEvalTime = now;
				return 0f;
			}

			float dt = (float)(now - _previewerRuntime.lastEvalTime);
			_previewerRuntime.lastEvalTime = now;

			if (dt < 0f)
				return 0f;

			return Mathf.Min(dt, MAX_EDITOR_DELTA_TIME);
		}

		private void LogExceptionThrottled(Exception exception)
		{
			double now = EditorApplication.timeSinceStartup;
			if (now - _previewerRuntime.lastExceptionLogTime < EXCEPTION_LOG_COOLDOWN_SEC)
				return;

			_previewerRuntime.lastExceptionLogTime = now;
			Debug.LogException(exception);
		}

		#endregion Methods
	}
}



