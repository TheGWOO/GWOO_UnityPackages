using System.Collections.Generic;
using GWOO.Editor.Utils;
using GWOO.UIElements;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// EditorWindow composition root. All non-UI logic lives in PreviewCore + components.
	/// </summary>
	public sealed class AnimatorPreviewerWindow : EditorWindow, IAnimatorPreviewerHost, IClipEditsResolver
	{
		#region Constants
		
		private const float MIN_WINDOW_WIDTH = 860f;
		private const float MIN_WINDOW_HEIGHT = 520f;

		private const int SPLIT_FIXED_PANE_INDEX = 0;
		private const int SPLIT_DEFAULT_LEFT_WIDTH = 560;
		
		#endregion Constants

		#region Fields
		
		[SerializeField] private AnimatorPreviewerState _previewerState = new();

		private readonly AnimatorPreviewerTheme _theme = new();
		public AnimatorPreviewerTheme Theme => _theme;

		private PreviewCore _core;

		// UI
		private TwoPaneSplitView _split;
		private VisualElement _leftRoot;

		private readonly HeaderPanel _headerPanel = new();
		private readonly ClipPreviewPanel _clipPanel = new();
		private readonly ControllerPreviewPanel _controllerPanel = new();
		private readonly AnimControllerRootPanel _rightPanel = new();

		private bool _built;
		private PreviewInvalidationFlags _pendingWindowInvalidation = PreviewInvalidationFlags.None;
		
		#endregion Fields
		
		#region Properties
		
		public Animator TargetAnimator => _previewerState.targetAnimator;
		public bool IsBound => _core != null && _core.IsBound;
		
		#endregion Properties
		
		#region Creation and Lifecycle
		
		[MenuItem("Window/Animation/Animator Previewer %#6")]
		private static void Open()
		{
			AnimatorPreviewerWindow window = GetWindow<AnimatorPreviewerWindow>("Animator Previewer");
			window.minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
			window.Show();
		}

		private void OnEnable()
		{
			wantsMouseMove = true;

			_core = new PreviewCore(_previewerState, this);
			AnimatorPreviewerSafetyHooks.Register(this);

			EditorApplication.update += OnEditorUpdate;
			Selection.selectionChanged += OnSelectionChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private void OnDisable()
		{
			Selection.selectionChanged -= OnSelectionChanged;
			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

			// avoid callback accumulation across CreateGUI rebuilds / domain reload quirks
			rootVisualElement?.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

			// UI-side responsibility: resolve edits if possible (no cancel on close).
			TryResolvePendingClipEdits("closing");

			AnimatorPreviewerSafetyHooks.Unregister(this);
			_core?.Unbind();
			_core = null;
		}

		private void CreateGUI()
		{
			_built = true;

			rootVisualElement.Clear();

			EditorCustomStyles.SetCustomStyleSheet(rootVisualElement, Palette.Dark);
			rootVisualElement.AddToClassList("lighter-background");

			rootVisualElement.style.flexDirection = FlexDirection.Column;
			rootVisualElement.style.flexGrow = 1f;
			rootVisualElement.focusable = true;

			_headerPanel.Build(rootVisualElement, this);
			rootVisualElement.Add(new Separator(1));

			BuildBody();

			rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
			rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

			rootVisualElement.SetEnabled(!EditorBusy.IsBusy());

			MarkUIDirty(PreviewInvalidationFlags.FullUI);
			RefreshUI(ConsumeUiInvalidation() | PreviewInvalidationFlags.FullUI);

			rootVisualElement.Focus();

			if (_previewerState.autoBindToSelection && _previewerState.targetAnimator != null)
				_core.QueueBind();
		}

		private void BuildBody()
		{
			_split = new TwoPaneSplitView(
				SPLIT_FIXED_PANE_INDEX,
				SPLIT_DEFAULT_LEFT_WIDTH,
				TwoPaneSplitViewOrientation.Horizontal)
			{
				style = { flexGrow = 1f }
			};

			rootVisualElement.Add(_split);

			_leftRoot = new VisualElement
			{
				style =
				{
					flexGrow = 1f,
					flexDirection = FlexDirection.Column,
					minWidth = 430,
					paddingLeft = 8,
					paddingRight = 8,
					paddingTop = 8,
					paddingBottom = 8
				}
			};

			VisualElement rightRoot = new();
			_rightPanel.Build(rightRoot, this);

			_split.Add(_leftRoot);
			_split.Add(rightRoot);

			_clipPanel.Build(_leftRoot, this);
			_controllerPanel.Build(_leftRoot, this);
		}

		#endregion Creation and Lifecycle

		#region Routing helpers

		private void FlushCoreInvalidation()
		{
			if (_core == null) return;

			PreviewInvalidationFlags flags = _core.ConsumeInvalidation();
			if (flags != PreviewInvalidationFlags.None)
				MarkUIDirty(flags);
		}

		private void Run(System.Action action)
		{
			if (_core == null) return;
			action?.Invoke();
			FlushCoreInvalidation();
		}

		private void Run<T1>(System.Action<T1> action, T1 a1)
		{
			if (_core == null) return;
			action?.Invoke(a1);
			FlushCoreInvalidation();
		}

		private void Run<T1, T2>(System.Action<T1, T2> action, T1 a1, T2 a2)
		{
			if (_core == null) return;
			action?.Invoke(a1, a2);
			FlushCoreInvalidation();
		}

		private TResult Run<TResult>(System.Func<TResult> func)
		{
			if (_core == null) return default;
			TResult result = func != null ? func.Invoke() : default;
			FlushCoreInvalidation();
			return result;
		}

		#endregion Routing helpers

		#region Editor callbacks

		private void OnEditorUpdate()
		{
			if (_core == null || !_built)
				return;

			PreviewInvalidationFlags flags = _core.Tick();
			flags |= ConsumeUiInvalidation();

			if (flags == PreviewInvalidationFlags.None)
				return;

			if ((flags & PreviewInvalidationFlags.Scene) != 0)
				SceneView.RepaintAll();

			RefreshUI(flags);
		}

		private void OnSelectionChanged()
		{
			if (_core == null || !_previewerState.autoBindToSelection || _core.IsBound)
				return;

			if (!PreviewSelection.TryGetSelectionAnimator(out Animator selectedAnimator))
				return;

			if (_previewerState.targetAnimator == selectedAnimator)
				return;

			Run(_core.SetTargetAnimator, selectedAnimator);
		}

		private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
		{
			switch (stateChange)
			{
				case PlayModeStateChange.EnteredEditMode:
					EditorBusy.Pop();
					break;
				case PlayModeStateChange.ExitingEditMode:
					EditorBusy.Push();
					break;
			}

			MarkUIDirty(PreviewInvalidationFlags.FullUI);
		}

		#endregion Editor callbacks

		#region UI refresh / invalidation

		private void RefreshUI(PreviewInvalidationFlags flags)
		{
			rootVisualElement.SetEnabled(!EditorBusy.IsBusy());

			_headerPanel.Refresh(this);

			bool isClipMode = _previewerState.mode == AnimatorPreviewerMode.Clip;

			if ((flags & PreviewInvalidationFlags.FullUI) != 0)
			{
				_clipPanel.SetVisible(isClipMode);
				_controllerPanel.SetVisible(!isClipMode);

				_clipPanel.Refresh(this);
				_controllerPanel.Refresh(this);
				_rightPanel.Refresh(this);
				return;
			}

			if (isClipMode)
			{
				if ((flags & PreviewInvalidationFlags.Playback) != 0)
					_clipPanel.RefreshPlaybackOnly(this);

				if ((flags & PreviewInvalidationFlags.Timeline) != 0)
					_clipPanel.RefreshTimelineOnly(this);
			}
			else
			{
				if ((flags & PreviewInvalidationFlags.Playback) != 0)
					_controllerPanel.RefreshPlaybackOnly(this);

				if ((flags & PreviewInvalidationFlags.ControllerContext) != 0)
					_controllerPanel.RefreshContextIfOpen(this);

				if ((flags & PreviewInvalidationFlags.RightPanelParams) != 0)
					_rightPanel.RefreshParamsOnly(this);
			}

			if ((flags & PreviewInvalidationFlags.RightPanelStates) != 0)
				_rightPanel.RefreshStatesOnly(this);
		}

		private void MarkUIDirty(PreviewInvalidationFlags flags)
		{
			_pendingWindowInvalidation |= flags;
			Repaint();
		}

		private PreviewInvalidationFlags ConsumeUiInvalidation()
		{
			PreviewInvalidationFlags f = _pendingWindowInvalidation;
			_pendingWindowInvalidation = PreviewInvalidationFlags.None;
			return f;
		}

		#endregion UI refresh / invalidation

		#region Input

		private void OnKeyDown(KeyDownEvent keyDownEvent)
		{
			if (keyDownEvent == null)
				return;

			if (IsTextEditingFocused())
				return;

			if (keyDownEvent.keyCode == KeyCode.Space)
			{
				if (!_core.IsBound)
					return;

				CmdTogglePlayPause();
				keyDownEvent.StopPropagation();
				return;
			}

			if (_previewerState.mode != AnimatorPreviewerMode.Clip || !_core.IsBound)
				return;

			if (keyDownEvent.keyCode == KeyCode.Home)
			{
				CmdResetTimeline();
				keyDownEvent.StopPropagation();
				return;
			}

			bool ctrlPressed = keyDownEvent.ctrlKey || keyDownEvent.commandKey;

			int frameStep;
			if (ctrlPressed)
				frameStep = _previewerState.fps;
			else if (keyDownEvent.shiftKey)
				frameStep = 10;
			else
				frameStep = 1;

			switch (keyDownEvent.keyCode)
			{
				case KeyCode.RightArrow:
					CmdStepFrames(frameStep);
					keyDownEvent.StopPropagation();
					break;
				case KeyCode.LeftArrow:
					CmdStepFrames(-frameStep);
					keyDownEvent.StopPropagation();
					break;
			}
		}

		private bool IsTextEditingFocused()
		{
			IPanel panel = rootVisualElement?.panel;
			if (panel?.focusController?.focusedElement is not VisualElement focusedElement)
				return false;

			for (VisualElement element = focusedElement; element != null; element = element.parent)
			{
				if (element is TextField
				    || element is IntegerField
				    || element is FloatField
				    || element is ToolbarSearchField)
					return true;

				if (element.ClassListContains("unity-base-text-field")
				    || element.ClassListContains("unity-text-input"))
					return true;
			}

			return false;
		}

		#endregion Input

		#region Focus helpers

		private void FocusSceneViewOnTarget()
		{
			if (_previewerState.targetAnimator == null)
				return;

			Selection.activeObject = _previewerState.targetAnimator.gameObject;

			SceneView sceneView = SceneView.lastActiveSceneView;
			if (sceneView == null && SceneView.sceneViews != null && SceneView.sceneViews.Count > 0)
				sceneView = SceneView.sceneViews[0] as SceneView;

			if (sceneView != null)
			{
				sceneView.FrameSelected();
				sceneView.Repaint();
				return;
			}

			SceneView.RepaintAll();
		}

		private static PendingEditsResolution GetDirtyPromptMode(bool allowCancel)
		{
			if (EditorBusy.IsBusy())
				return PendingEditsResolution.ApplyRevertOnly;

			return allowCancel
				? PendingEditsResolution.Cancelable
				: PendingEditsResolution.ApplyRevertOnly;
		}

		public bool TryResolvePendingClipEdits(string context, PendingEditsResolution resolution = PendingEditsResolution.ApplyRevertOnly)
		{
			if (_clipPanel == null)
				return true;

			PendingEditsResolution effective =
				resolution == PendingEditsResolution.Cancelable
					? GetDirtyPromptMode(allowCancel: true)
					: resolution;

			return _clipPanel.TryResolveUnappliedEdits(context, effective);
		}

		#endregion Focus helpers

		#region Safety hooks

		internal void SafetyUnbind(string reason, bool clearAnimatorField)
		{
			if (_core == null) return;
			Run(_core.SafetyUnbind, reason, clearAnimatorField);
		}

		internal void SafetyRestorePoseSnapshot(string reason)
		{
			if (_core == null) return;
			Run(_core.SafetyRestorePoseSnapshot, reason);
		}

		internal void SafetyRestorePreview(string reason)
		{
			if (_core == null) return;
			Run(_core.SafetyRestorePreview, reason);
		}

		#endregion Safety hooks

		#region Core host

		public AnimatorPreviewerViewState GetViewState() => _core.BuildViewState();

		public void CmdSetTargetAnimator(Animator animator)
		{
			if (_core.IsBound && animator != _previewerState.targetAnimator)
			{
				if (!TryResolvePendingClipEdits("switching target animator"))
					return;
			}

			Run(_core.SetTargetAnimator, animator);
		}

		public void CmdToggleBind()
		{
			if (_core.IsBound)
			{
				if (!TryResolvePendingClipEdits("unbinding", PendingEditsResolution.Cancelable))
					return;

				Run(_core.Unbind);
				return;
			}

			Run(_core.Bind);
		}

		public void CmdFocusSceneView() => FocusSceneViewOnTarget();

		public bool CmdSetMode(AnimatorPreviewerMode mode)
		{
			if (_core.IsBound && mode != _previewerState.mode)
			{
				if (!TryResolvePendingClipEdits("switching mode", PendingEditsResolution.Cancelable))
					return false;
			}

			return Run(() => _core.SetMode(mode));
		}

		public void CmdSetAutoBind(bool enabled) => Run(_core.SetAutoBindToSelection, enabled);
		public void CmdSetLockPos(bool enabled) => Run(_core.SetLockRootPosition, enabled);
		public void CmdSetLockRot(bool enabled) => Run(_core.SetLockRootRotation, enabled);

		public bool CmdSetPreviewClip(AnimationClip clip)
		{
			if (_core.IsBound)
			{
				if (!TryResolvePendingClipEdits("switching preview clip"))
					return false;
			}

			Run(_core.SetPreviewClip, clip);
			return true;
		}

		#endregion Core host

		#region ClipTimeline

		public void CmdTimelineScrubStart(float timeSec) => Run(_core.ClipTimeline.ScrubStart, timeSec);
		public void CmdTimelineScrubMove(float timeSec) => Run(_core.ClipTimeline.ScrubMove, timeSec);
		public void CmdTimelineScrubEnd(float timeSec) => Run(_core.ClipTimeline.ScrubEnd, timeSec);
		public void CmdTimelineLoopRangeChanged(float a, float b) => Run(_core.ClipTimeline.OnTimelineLoopRangeChanged, a, b);
		public void CmdTimelineClearLoopRange() => Run(_core.ClipTimeline.ClearLoopRange);
		public void CmdStepFrames(int frames) => Run(_core.ClipTimeline.StepFrames, frames);
		public void CmdResetTimeline() => Run(_core.ClipTimeline.ResetTimeline);
		public void CmdTogglePlayPause() => Run(_core.ClipTimeline.TogglePlayPause);

		public void CmdSetFps(int fps) => Run(_core.SetFps, fps);
		public void CmdSetUseClipLength(bool enabled) => Run(_core.SetUseClipLength, enabled);
		public void CmdSetSnapLengthToFps(bool enabled) => Run(_core.SetSnapLengthToFps, enabled);
		public void CmdSetCustomTimelineLength(float seconds) => Run(_core.SetCustomTimelineLength, seconds);
		public void CmdSetTimeScale(float value) => Run(_core.SetTimeScale, value);
		public void CmdSetLoop(bool value) => Run(_core.SetLoop, value);
		public void CmdSetEventsEnabled(bool enabled) => Run(_core.SetEventsEnabled, enabled);
		public void CmdSetLogFiredEvents(bool enabled) => Run(_core.SetLogFiredEvents, enabled);
		public void CmdSetDrawEventMarkers(bool enabled) => Run(_core.SetDrawEventMarkers, enabled);
		public void CmdSetEventClipWeightThreshold(float value) => Run(_core.SetEventClipWeightThreshold, value);

		#endregion ClipTimeline

		#region Clip Events

		public AnimationEvent[] GetClipEventsSafe(AnimationClip clip) => ClipEventsUtility.GetClipEventsSafe(clip);

		public bool TryApplyClipEvents(AnimationClip clip, AnimationEvent[] eventsToWrite, string undoLabel, out AnimationClip refreshedClip)
		{
			return _core.TryApplyClipEvents(clip, eventsToWrite, undoLabel, out refreshedClip);
		}

		public AnimationClip TryRefreshClipReference(AnimationClip clip) => ClipEventsUtility.TryRefreshClipReference(clip);

		#endregion Clip Events

		#region Animation States

		public IReadOnlyList<AnimatorPreviewerStateEntry> GetVisibleStates() => _core.AnimationStates.GetVisibleStates();

		public void CmdSetStateSearch(string query) => Run(_core.SetStateSearch, query);
		public void CmdSetStateDisplayFlag(AnimatorPreviewerStateDisplayFlags flag, bool enabled) => Run(_core.SetStateDisplayFlag, flag, enabled);
		public void CmdRefreshStates() => Run(_core.RefreshStates);
		public void CmdSetSelectedState(int layerIndex, int stateHash) => Run(_core.SetSelectedState, layerIndex, stateHash);

		public void CmdPreviewSelectedStateClip(Motion motion)
		{
			if (!TryResolvePendingClipEdits("previewing state clip", PendingEditsResolution.Cancelable))
				return;

			string error = string.Empty;

			bool ok = Run(() => _core.TryPreviewSelectedStateClip(motion, out error));

			if (ok)
				return;

			if (!string.IsNullOrEmpty(error))
				EditorUtility.DisplayDialog("Preview Clip", error, "OK");
		}

		public void CmdPlaySelectedStateController(int layerIndex, int stateHash)
		{
			if (!TryResolvePendingClipEdits("playing state in controller", PendingEditsResolution.Cancelable))
				return;

			Run(_core.ControllerDriver.PlayStateController, layerIndex, stateHash);
		}

		#endregion Animation States

		#region Animation Controller/Parameters

		public AnimatorController GetActiveController() => _core.ControllerDriver.ActiveController;

		public void CmdSetControllerOverride(AnimatorController controller)
		{
			if (_core.IsBound && controller != _previewerState.controllerOverride)
			{
				if (!TryResolvePendingClipEdits("switching controller override", PendingEditsResolution.Cancelable))
					return;
			}

			Run(_core.SetControllerOverride, controller);
		}

		public void CmdSetAutoRebindOnAssetChanges(bool enabled) => Run(_core.SetAutoRebindOnAssetChanges, enabled);

		public bool TryGetLayerWeight(int layerIndex, out float weight) => _core.ControllerDriver.TryGetLayerWeight(layerIndex, out weight);
		public void CmdSetLayerWeight(int layerIndex, float weight) => Run(_core.ControllerDriver.SetLayerWeight, layerIndex, weight);

		public bool TryGetFloat(int hash, out float v) => _core.ControllerDriver.TryGetFloat(hash, out v);
		public bool TryGetInt(int hash, out int v) => _core.ControllerDriver.TryGetInt(hash, out v);
		public bool TryGetBool(int hash, out bool v) => _core.ControllerDriver.TryGetBool(hash, out v);

		public void CmdSetFloat(int hash, float v) => Run(_core.ControllerDriver.SetFloat, hash, v);
		public void CmdSetInt(int hash, int v) => Run(_core.ControllerDriver.SetInt, hash, v);
		public void CmdSetBool(int hash, bool v) => Run(_core.ControllerDriver.SetBool, hash, v);

		public void CmdSetTrigger(int hash) => Run(_core.ControllerDriver.SetTrigger, hash);
		public void CmdResetTrigger(int hash) => Run(_core.ControllerDriver.ResetTrigger, hash);

		public void CmdPreviewContextClip(int clipInstanceId, float normalizedTime)
		{
			if (!TryResolvePendingClipEdits("previewing context clip", PendingEditsResolution.Cancelable))
				return;

			Run(() => _core.ControllerDriver.PreviewContextClip(clipInstanceId, normalizedTime, _core.ClipTimeline));
		}

		public void QueryControllerContext(List<AnimatorPreviewerControllerLayerContext> buffer) => _core.ControllerDriver.QueryControllerContext(buffer);

		public string ResolveStateName(int layer, int fullPathHash) => _core.AnimationStates.ResolveStateName(layer, fullPathHash);

		#endregion Animation Controller/Parameters
	}
}
