using System;
using GWOO.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GWOO.Editor.Tools
{
	internal sealed class ClipPreviewPanel : IAnimatorPreviewerPanel
	{
		#region Properties

		public VisualElement Root { get; private set; }

		#endregion Properties

		#region Fields

		private VisualElement _panel;

		private ScrollView _scroll;
		private ObjectField _clipField;
		private IntegerField _fpsField;
		private Toggle _useClipLengthToggle;
		private Toggle _snapLengthToggle;
		private FloatField _customLenField;

		private EventsBlock _eventsBlock;

		private TimelineBarElement _timelineBar;
		private Label _timelineLabel;

		private VisualElement _timelineGroup;
		
		// Note: We don't really need to store the dot if we use the Card API correctly, 
		// but keeping field structure consistent with previous code for safety.
		// Actually, let's remove unused fields to be clean as per "Clean Code Hygiene".
		// private VisualElement _timelineGroupDot; 

		private PlaybackBlock _playbackBlock;

		private VisualElement _editRow;
		private ToolbarToggle _editEventsToggle;
		private bool _ignoreEditToggle;

		private VisualElement _clipEventsGroup;
		private EditEventsBlock _editEventsBlock;

		private CustomButton _stepBwd1S;
		private CustomButton _stepBwd10F;
		private CustomButton _stepBwd1F;
		private CustomButton _stepFwd1F;
		private CustomButton _stepFwd10F;
		private CustomButton _stepFwd1S;
		private CustomButton _resetButton;

		private AnimationClip _lastClip;
		private bool _ignoreClipFieldCallback;
		private bool _detachPrompted;

		private AnimationEvent[] _cachedClipEvents = Array.Empty<AnimationEvent>();
		private int _cachedClipId;
		private double _cachedEventsTime;
		private const double EVENTS_CACHE_REFRESH_SEC = 0.25;

		private readonly CallbackScope _callbacks = new();

		private bool _built;
		private IAnimatorPreviewerHost _host;

		private EventCallback<DetachFromPanelEvent> _onDetachCb;

		#endregion Fields

		#region Public Methods

		internal bool HasUnappliedEdits =>
			IsEditModeActive() && _editEventsBlock != null && _editEventsBlock.HasUnappliedChanges;

		internal bool TryResolveUnappliedEdits(string context, PendingEditsResolution mode)
		{
			if (!HasUnappliedEdits)
				return true;

			if (mode == PendingEditsResolution.SilentRevert)
			{
				_editEventsBlock.Revert();
				InvalidateEventsCache();
				RefreshTimelineOnly(_host);
				ForceExitEditMode_NoPrompt();
				return true;
			}

			string msg = $"You have unapplied changes to clip events.\n\nApply or Revert before {context}?";

			if (mode == PendingEditsResolution.ApplyRevertOnly)
			{
				bool apply = EditorUtility.DisplayDialog(
					"Unapplied Clip Event Changes",
					msg,
					"Apply",
					"Revert");

				if (apply) _editEventsBlock.Apply();
				else _editEventsBlock.Revert();

				InvalidateEventsCache();
				RefreshTimelineOnly(_host);
				ForceExitEditMode_NoPrompt();
				return true;
			}

			int rc = EditorUtility.DisplayDialogComplex(
				"Unapplied Clip Event Changes",
				msg,
				"Apply",
				"Revert",
				"Cancel");

			switch (rc)
			{
				case 2:
					return false;
				case 0:
				{
					_editEventsBlock.Apply();
					if (_editEventsBlock.HasUnappliedChanges)
						return false;
					break;
				}
				default:
					_editEventsBlock.Revert();
					break;
			}

			InvalidateEventsCache();
			RefreshTimelineOnly(_host);
			ForceExitEditMode_NoPrompt();
			return true;
		}

		public void Build(VisualElement parent, IAnimatorPreviewerHost host)
		{
			Dispose();

			_host = host;

			AnimatorPreviewerTheme t = host.Theme;

			_panel = new Card("Clip Preview", t.accentClip);
			_panel.style.flexGrow = 1f;

			_onDetachCb = _ =>
			{
				if (_detachPrompted) return;
				_detachPrompted = true;

				TryResolveUnappliedEdits("closing / detaching", PendingEditsResolution.ApplyRevertOnly);
			};
			_panel.RegisterCallback(_onDetachCb);
			_callbacks.Add(() =>
			{
				if (_panel != null && _onDetachCb != null)
					_panel.UnregisterCallback(_onDetachCb);
			});

			_scroll = new ScrollView(ScrollViewMode.Vertical)
			{
				style =
				{
					flexGrow = 1f,
					paddingRight = 2
				}
			};
			_panel.Add(_scroll);

			BuildSettingsGroup(_scroll, host);
			BuildEventsGroup(_scroll, host);
			BuildTimelineGroup(_scroll, host);
			BuildClipEventsGroup(_scroll, host);

			Root = _panel;
			parent.Add(Root);

			_lastClip = null;

			SetEditModeUI(false);
			_built = true;
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			_host = host;

			if (!_built || Root == null)
				return;

			AnimatorPreviewerViewState s = host.GetViewState();
			if (!s.isBound)
			{
				Root.SetEnabled(false);
				return;
			}

			Root.SetEnabled(true);

			AnimatorPreviewerTheme t = host.Theme;

			bool isClipMode = s.mode == AnimatorPreviewerMode.Clip;
			AnimationClip currentClip = s.previewClip;

			_ignoreClipFieldCallback = true;
			_clipField.SetValueWithoutNotify(currentClip);
			_ignoreClipFieldCallback = false;

			if (currentClip != _lastClip)
				InvalidateEventsCache();

			_lastClip = currentClip;

			_useClipLengthToggle.SetEnabled(currentClip != null);
			_snapLengthToggle.SetEnabled(currentClip != null);
			_customLenField.SetEnabled(currentClip != null && !s.useClipLength);

			_fpsField.SetValueWithoutNotify(s.fps);
			_useClipLengthToggle.SetValueWithoutNotify(s.useClipLength);
			_snapLengthToggle.SetValueWithoutNotify(s.snapLengthToFps);
			_customLenField.SetValueWithoutNotify(s.customTimelineLength);

			_eventsBlock.SetEnabledValue(s.eventsEnabled);
			_eventsBlock.SetClipFields(s.drawEventMarkers);
			_eventsBlock.SetLogValue(s.logFiredEvents);

			_stepBwd1S.SetEnabled(isClipMode);
			_stepBwd10F.SetEnabled(isClipMode);
			_stepBwd1F.SetEnabled(isClipMode);
			_stepFwd1F.SetEnabled(isClipMode);
			_stepFwd10F.SetEnabled(isClipMode);
			_stepFwd1S.SetEnabled(isClipMode);
			_resetButton.SetEnabled(isClipMode);

			_timelineBar.SetEnabled(isClipMode);

			_editEventsToggle.SetEnabled(isClipMode && currentClip != null);

			_editEventsBlock?.Refresh();

			RefreshTimelineOnly(host);

			_playbackBlock.Refresh(
				playing: s.isPlaying && isClipMode,
				speed: s.timeScale,
				loop: s.loop,
				playAccent: t.accentClip,
				pauseAccent: t.pauseOrange);
		}
		
		public void RefreshTimelineOnly(IAnimatorPreviewerHost host)
		{
			if (_timelineBar == null || _timelineLabel == null) return;
			if (host == null) return;

			AnimatorPreviewerViewState s = host.GetViewState();
			AnimatorPreviewerTheme t = host.Theme;

			int fps = Mathf.Max(1, s.fps);
			float frameDur = 1f / fps;

			float timelineLen = Mathf.Max(0.033f, s.timelineLength);
			float displayTime = Mathf.Clamp((float)s.timelineTime, 0f, timelineLen);

			int totalFrames = Mathf.Max(1, Mathf.RoundToInt(timelineLen * fps));
			int curFrame = Mathf.Clamp(Mathf.RoundToInt(displayTime / frameDur), 0, totalFrames);

			bool hasRange = s.loopRangeEnabled && (s.loopRangeEndSec > s.loopRangeStartSec + (1f / fps));
			string rangeLabel = hasRange
				? $" | Range: {s.loopRangeStartSec:0.###}–{s.loopRangeEndSec:0.###}s"
				: " | Range: (none) [RMB drag]";

			_timelineLabel.text = $"Time: {displayTime:0.###}s | Frame: {curFrame}/{totalFrames} | Length: {timelineLen:0.###}s{rangeLabel}";

			_timelineBar.backgroundColor = t.timelineBg;
			_timelineBar.borderColor = t.timelineBorder;
			_timelineBar.tickColor = t.timelineTicks;
			_timelineBar.playheadColor = t.playhead;
			_timelineBar.rangeAccent = t.accentClip;
			_timelineBar.eventMarkerColor = t.eventMarker;
			_timelineBar.eventMarkerHoverColor = t.eventMarkerHover;

			_timelineBar.timelineLengthSec = timelineLen;
			_timelineBar.playheadTimeSec = displayTime;
			_timelineBar.fps = fps;

			bool isEditing = IsEditModeActive() && _editEventsBlock != null;
			bool canMarkers = s.previewClip != null && (isEditing || (s.eventsEnabled && s.drawEventMarkers));
			_timelineBar.drawEventMarkers = canMarkers;

			AnimationEvent[] eventsSrc = null;
			if (isEditing)
			{
				eventsSrc = _editEventsBlock.GetWorkingEventsForTimeline();
			}
			else if (canMarkers)
			{
				eventsSrc = GetSafeCachedEvents(host, s.previewClip);
			}

			_timelineBar.clipEvents = canMarkers ? eventsSrc : null;
			_timelineBar.clipLengthSec = s.previewClip != null ? Mathf.Max(1e-6f, s.previewClip.length) : 1f;

			_timelineBar.hasLoopRange = hasRange;
			_timelineBar.loopRangeStartSec = s.loopRangeStartSec;
			_timelineBar.loopRangeEndSec = s.loopRangeEndSec;

			_timelineBar.MarkDirtyRepaint();
		}

		public void Dispose()
		{
			_built = false;

			_callbacks.Clear();

			this.SafelyRemovePanel();

			_cachedClipEvents = Array.Empty<AnimationEvent>();
			_cachedClipId = 0;
			_cachedEventsTime = 0;

			_detachPrompted = false;
			_ignoreEditToggle = false;
			_ignoreClipFieldCallback = false;

			_lastClip = null;

			_onDetachCb = null;

			_resetButton = null;
			_stepFwd1S = null;
			_stepFwd10F = null;
			_stepFwd1F = null;
			_stepBwd1F = null;
			_stepBwd10F = null;
			_stepBwd1S = null;

			_editEventsBlock = null;
			_clipEventsGroup = null;

			_editEventsToggle = null;
			_editRow = null;

			_playbackBlock = null;

			_timelineLabel = null;
			_timelineBar = null;
			_timelineGroup = null;

			_eventsBlock = null;

			_customLenField = null;
			_snapLengthToggle = null;
			_useClipLengthToggle = null;
			_fpsField = null;
			_clipField = null;

			_scroll = null;
			_panel = null;

			Root = null;
			_host = null;
		}

		public void SetVisible(bool visible)
		{
			PanelCard.SetDisplay(Root, visible);
		}

		#endregion Public Methods

		#region Private Methods

		private void BuildSettingsGroup(VisualElement parent, IAnimatorPreviewerHost host)
		{
			AnimatorPreviewerTheme t = host.Theme;

			VisualElement settings = new Card("Preview Settings", t.accentClip, isGroup: true);
			parent.Add(settings);

			_clipField = settings.CreateAndBind<ObjectField, Object>(
				new ObjectField("Preview Clip") { objectType = typeof(AnimationClip), allowSceneObjects = false },
				v => OnClipFieldChanged(v as AnimationClip),
				_callbacks);

			_fpsField = settings.CreateAndBind<IntegerField, int>(
				new IntegerField("FPS"),
				v => _host?.CmdSetFps(v),
				_callbacks);

			_useClipLengthToggle = settings.CreateAndBind<Toggle, bool>(
				new Toggle("Use Clip Length") { tooltip = "If checked, the timeline will be set to the length of the preview clip." },
				v => _host?.CmdSetUseClipLength(v),
				_callbacks);

			_customLenField = settings.CreateAndBind<FloatField, float>(
				new FloatField("Custom Length (s)") { tooltip = "The length to use when using custom timeline length." },
				v => _host?.CmdSetCustomTimelineLength(v),
				_callbacks);

			_snapLengthToggle = settings.CreateAndBind<Toggle, bool>(
				new Toggle("Snap Length to FPS") { tooltip = "If checked, the length of the timeline will be rounded to the nearest frame." },
				v => _host?.CmdSetSnapLengthToFps(v),
				_callbacks);
		}

		private void OnClipFieldChanged(AnimationClip newClip)
		{
			if (_ignoreClipFieldCallback || _host == null)
				return;

			AnimationClip prev = _host.GetViewState().previewClip;

			if (_host.CmdSetPreviewClip(newClip))
				return;

			_ignoreClipFieldCallback = true;
			_clipField.SetValueWithoutNotify(prev);
			_ignoreClipFieldCallback = false;
		}

		private void BuildEventsGroup(VisualElement parent, IAnimatorPreviewerHost host)
		{
			AnimatorPreviewerTheme t = host.Theme;

			VisualElement events = new Card("Events (Previewer)", t.accentClip, isGroup: true);
			parent.Add(events);

			_eventsBlock = new EventsBlock(isClipMode: true);
			Action eventsChanged = OnEventsBlockChanged;
			_eventsBlock.OnChanged += eventsChanged;
			_callbacks.Add(() =>
			{
				if (_eventsBlock != null)
					_eventsBlock.OnChanged -= eventsChanged;
			});
			events.Add(_eventsBlock);
		}

		private void BuildTimelineGroup(VisualElement parent, IAnimatorPreviewerHost host)
		{
			AnimatorPreviewerTheme t = host.Theme;

			_timelineGroup = new Card("Timeline", t.accentClip, isGroup: true);
			parent.Add(_timelineGroup);

			_timelineBar = new TimelineBarElement
			{
				style =
				{
					height = 60,
					flexShrink = 0
				}
			};

			_timelineBar.ScrubStarted += OnTimelineScrubStart;
			_timelineBar.Scrubbed += OnTimelineScrubMove;
			_timelineBar.ScrubEnded += OnTimelineScrubEnd;

			_timelineBar.LoopRangeChanged += OnTimelineLoopRangeChanged;
			_timelineBar.LoopRangeCleared += OnTimelineLoopRangeCleared;

			_timelineBar.EventSelected += OnTimelineEventSelected;
			_timelineBar.EventDragStarted += OnTimelineEventDragStarted;
			_timelineBar.EventDragged += OnTimelineEventDragged;
			_timelineBar.EventDragEnded += OnTimelineEventDragEnded;

			_callbacks.Add(() =>
			{
				if (_timelineBar == null) return;

				_timelineBar.ScrubStarted -= OnTimelineScrubStart;
				_timelineBar.Scrubbed -= OnTimelineScrubMove;
				_timelineBar.ScrubEnded -= OnTimelineScrubEnd;

				_timelineBar.LoopRangeChanged -= OnTimelineLoopRangeChanged;
				_timelineBar.LoopRangeCleared -= OnTimelineLoopRangeCleared;

				_timelineBar.EventSelected -= OnTimelineEventSelected;
				_timelineBar.EventDragStarted -= OnTimelineEventDragStarted;
				_timelineBar.EventDragged -= OnTimelineEventDragged;
				_timelineBar.EventDragEnded -= OnTimelineEventDragEnded;
			});

			_timelineGroup.Add(_timelineBar);

			_timelineLabel = new Label
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					marginTop = 6,
					opacity = 0.85f
				}
			};
			_timelineGroup.Add(_timelineLabel);

			BuildTimelineStepRow(_timelineGroup);
			BuildPlaybackBlock(_timelineGroup);
			BuildEditRow(_timelineGroup);
		}

		private void BuildTimelineStepRow(VisualElement parent)
		{
			VisualElement stepRow = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexWrap = Wrap.Wrap,
					marginTop = 8
				}
			};
			parent.Add(stepRow);

			_stepBwd1S = PanelCard.NewRowButton("<-1s", () => _host?.CmdStepFrames(-_host.GetViewState().fps), 52);
			_stepBwd10F = PanelCard.NewRowButton("<<", () => _host?.CmdStepFrames(-10), 46);
			_stepBwd1F = PanelCard.NewRowButton("<", () => _host?.CmdStepFrames(-1), 40);
			_stepFwd1F = PanelCard.NewRowButton(">", () => _host?.CmdStepFrames(1), 40);
			_stepFwd10F = PanelCard.NewRowButton(">>", () => _host?.CmdStepFrames(10), 46);
			_stepFwd1S = PanelCard.NewRowButton("1s->", () => _host?.CmdStepFrames(_host.GetViewState().fps), 52);
			_resetButton = PanelCard.NewRowButton("Reset", () => _host?.CmdResetTimeline(), 60, marginRight: 0);

			_stepBwd1S.tooltip = "Ctrl+Left: Step backward by 1 second (FPS frames).";
			_stepBwd10F.tooltip = "Shift+Left: Step backward by 10 frames.";
			_stepBwd1F.tooltip = "Left: Step backward by 1 frame.";
			_stepFwd1F.tooltip = "Right: Step forward by 1 frame.";
			_stepFwd10F.tooltip = "Shift+Right: Step forward by 10 frames.";
			_stepFwd1S.tooltip = "Ctrl+Right: Step forward by 1 second (FPS frames).";
			_resetButton.tooltip = "Home: Reset playhead to start (or loop range start if set).";

			stepRow.Add(_stepBwd1S);
			stepRow.Add(_stepBwd10F);
			stepRow.Add(_stepBwd1F);

			stepRow.Add(PanelCard.Spacer(8, horizontal: true));

			stepRow.Add(_stepFwd1F);
			stepRow.Add(_stepFwd10F);
			stepRow.Add(_stepFwd1S);

			stepRow.Add(PanelCard.Spacer(8, horizontal: true));

			stepRow.Add(_resetButton);
		}

		private void BuildPlaybackBlock(VisualElement parent)
		{
			_playbackBlock = new PlaybackBlock(showLoop: true);

			Action playPauseCb = () => _host?.CmdTogglePlayPause();
			Action<float> speedCb = v => _host?.CmdSetTimeScale(v);
			Action<bool> loopCb = v => _host?.CmdSetLoop(v);

			_playbackBlock.OnPlayPause += playPauseCb;
			_playbackBlock.OnSpeedChanged += speedCb;
			_playbackBlock.OnLoopChanged += loopCb;

			_callbacks.Add(() =>
			{
				if (_playbackBlock == null) return;
				_playbackBlock.OnPlayPause -= playPauseCb;
				_playbackBlock.OnSpeedChanged -= speedCb;
				_playbackBlock.OnLoopChanged -= loopCb;
			});

			parent.Add(PanelCard.Spacer(8, horizontal: false));
			parent.Add(_playbackBlock);
		}

		private void BuildEditRow(VisualElement parent)
		{
			_editRow = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexWrap = Wrap.Wrap,
					marginTop = 10,
					paddingLeft = 6,
					paddingRight = 6,
					paddingTop = 4,
					paddingBottom = 4,
					borderTopLeftRadius = 6,
					borderTopRightRadius = 6,
					borderBottomLeftRadius = 6,
					borderBottomRightRadius = 6
				}
			};
			parent.Add(_editRow);

			_editEventsToggle = _editRow.CreateAndBind<ToolbarToggle, bool>(
				new ToolbarToggle { text = "Edit Events", tooltip = "Enable event editing (staged). Use Apply/Revert to commit." },
				OnEditToggleChanged,
				_callbacks);
		}

		private void OnEditToggleChanged(bool newValue)
		{
			if (_ignoreEditToggle) return;
			if (_host == null) return;

			if (!newValue)
			{
				if (_editEventsBlock != null && _editEventsBlock.HasUnappliedChanges)
				{
					int r = EditorUtility.DisplayDialogComplex(
						"Unapplied Clip Event Changes",
						"You have unapplied changes to clip events.\n\nApply or Revert before leaving Edit mode?",
						"Apply",
						"Revert",
						"Cancel");

					switch (r)
					{
						case 2:
							SetEditToggleValueNoNotify(true);
							return;
						case 0:
							_editEventsBlock.Apply();
							break;
						default:
							_editEventsBlock.Revert();
							break;
					}

					InvalidateEventsCache();
				}

				SetEditModeUI(false);
				RefreshTimelineOnly(_host);
				return;
			}

			SetEditModeUI(true);
			RefreshTimelineOnly(_host);
		}

		private void BuildClipEventsGroup(VisualElement parent, IAnimatorPreviewerHost host)
		{
			AnimatorPreviewerTheme t = host.Theme;

			_clipEventsGroup = new Card("Clip Events", t.accentClip, isGroup: true);
			parent.Add(_clipEventsGroup);

			// Note: Accent color control via Card property in SetEditModeUI

			_editEventsBlock = new EditEventsBlock(host);
			Action<bool> timeDragCb = OnEditBlockTimeDragChanged;
			_editEventsBlock.OnTimeDragStateChanged += timeDragCb;
			_editEventsBlock.OnTimelineMarkersDirty += OnMarkersDirtyFromEditor;
			_callbacks.Add(() =>
			{
				if (_editEventsBlock == null)
					return;
				
				_editEventsBlock.OnTimeDragStateChanged -= timeDragCb;
				_editEventsBlock.OnTimelineMarkersDirty -= OnMarkersDirtyFromEditor;
			});

			_clipEventsGroup.Add(_editEventsBlock);
			PanelCard.SetDisplay(_clipEventsGroup, false);
		}

		private void OnEventsBlockChanged()
		{
			if (_host == null || _eventsBlock == null) return;

			_host.CmdSetEventsEnabled(_eventsBlock.GetEnabledValue());
			_host.CmdSetDrawEventMarkers(_eventsBlock.GetDrawMarkers());
			_host.CmdSetLogFiredEvents(_eventsBlock.GetLogValue());
		}

		private void OnEditToggleChanged(ChangeEvent<bool> evt)
		{
			// Legacy overload just in case, but unused by new binder
			OnEditToggleChanged(evt.newValue);
		}

		private void SetEditToggleValueNoNotify(bool v)
		{
			if (_editEventsToggle == null)
				return;

			_ignoreEditToggle = true;
			_editEventsToggle.SetValueWithoutNotify(v);
			_ignoreEditToggle = false;
		}

		private void OnEditBlockTimeDragChanged(bool active)
		{
			if (_timelineBar == null)
				return;

			_timelineBar.drawPlayhead = !active;
			_timelineBar.MarkDirtyRepaint();
		}

		private void OnMarkersDirtyFromEditor()
		{
			if (_timelineBar == null || _host == null)
				return;

			if (!IsEditModeActive() || _editEventsBlock == null)
			{
				RefreshTimelineOnly(_host);
				return;
			}

			_timelineBar.clipEvents = _editEventsBlock.GetWorkingEventsForTimeline();
			_timelineBar.selectedEventIndex = _editEventsBlock.SelectedEventIndex;
			_timelineBar.MarkDirtyRepaint();
		}

		private void OnTimelineScrubStart(float t) => _host?.CmdTimelineScrubStart(t);
		private void OnTimelineScrubMove(float t) => _host?.CmdTimelineScrubMove(t);
		private void OnTimelineScrubEnd(float t) => _host?.CmdTimelineScrubEnd(t);

		private void OnTimelineLoopRangeChanged(float a, float b) => _host?.CmdTimelineLoopRangeChanged(a, b);
		private void OnTimelineLoopRangeCleared() => _host?.CmdTimelineClearLoopRange();

		private void OnTimelineEventSelected(int idx)
		{
			if (IsEditModeActive() && _editEventsBlock != null)
				_editEventsBlock.SelectEvent(idx);
		}

		private void OnTimelineEventDragStarted(int idx, float clipT)
		{
			_editEventsBlock?.BeginExternalTimeDrag();
			OnMarkerDrag(_host, clipT,true,false, idx);
		}

		private void OnTimelineEventDragged(int idx, float clipT)
		{
			OnMarkerDrag(_host, clipT,false,false, idx);
		}

		private void OnTimelineEventDragEnded(int idx, float clipT)
		{
			OnMarkerDrag(_host, clipT,false,true, idx);
			_editEventsBlock?.EndExternalTimeDrag();
		}

		private bool IsEditModeActive() => _editEventsToggle != null && _editEventsToggle.value;

		private void ForceExitEditMode_NoPrompt()
		{
			if (_editEventsToggle == null)
				return;

			if (!_editEventsToggle.value)
				return;

			SetEditToggleValueNoNotify(false);
			SetEditModeUI(false);
		}

		private void SetEditModeUI(bool edit)
		{
			if (_host == null) return;

			AnimatorPreviewerTheme t = _host.Theme;
			Color danger = t.editWarning;

			if (_timelineBar != null)
			{
				_timelineBar.editEventsMode = edit;
				_timelineBar.drawPlayhead = true;
			}

			PanelCard.SetDisplay(_clipEventsGroup, edit);

			if (edit)
			{
				if (_timelineGroup is Card tCard)
					tCard.AccentColor = danger;

				_timelineGroup.style.borderLeftWidth = 2;
				_timelineGroup.style.borderLeftColor = new StyleColor(new Color(danger.r, danger.g, danger.b, 0.80f));

				_editRow.style.backgroundColor = new StyleColor(new Color(danger.r, danger.g, danger.b, 0.12f));

				_editEventsToggle.text = "EDIT EVENTS";
				_editEventsToggle.style.unityFontStyleAndWeight = FontStyle.Bold;

				if (_clipEventsGroup is Card cCard)
					cCard.AccentColor = danger;

				_clipEventsGroup.style.borderLeftWidth = 2;
				_clipEventsGroup.style.borderLeftColor = new StyleColor(new Color(danger.r, danger.g, danger.b, 0.80f));
			}
			else
			{
				if (_timelineGroup is Card tCard)
					tCard.AccentColor = t.accentClip;

				_timelineGroup.style.borderLeftWidth = 0;
				_timelineGroup.style.borderLeftColor = StyleKeyword.Null;

				_editRow.style.backgroundColor = StyleKeyword.Null;

				_editEventsToggle.text = "Edit Events";
				_editEventsToggle.style.unityFontStyleAndWeight = FontStyle.Normal;

				if (_clipEventsGroup is Card cCard)
					cCard.AccentColor = t.accentClip;

				_clipEventsGroup.style.borderLeftWidth = 0;
				_clipEventsGroup.style.borderLeftColor = StyleKeyword.Null;

				if (_timelineBar != null)
				{
					_timelineBar.selectedEventIndex = -1;
					_timelineBar.drawPlayhead = true;
				}
			}
		}

		private void OnMarkerDrag(IAnimatorPreviewerHost host, float clipTimeSec, bool start, bool end, int idx)
		{
			if (host == null) return;
			if (!IsEditModeActive() || _editEventsBlock == null)
				return;

			_editEventsBlock.NotifyExternalEventTimeChange(idx, clipTimeSec);

			AnimatorPreviewerViewState s = host.GetViewState();
			AnimationClip clip = s.previewClip;
			if (clip == null) return;

			float clipLen = Mathf.Max(1e-6f, clip.length);
			float tlLen = Mathf.Max(1e-6f, s.timelineLength);

			float tlT = Mathf.Clamp((clipTimeSec / clipLen) * tlLen, 0f, tlLen);

			if (start) host.CmdTimelineScrubStart(tlT);
			else if (end) host.CmdTimelineScrubEnd(tlT);
			else host.CmdTimelineScrubMove(tlT);
		}

		internal void RefreshPlaybackOnly(IAnimatorPreviewerHost host)
		{
			if (_playbackBlock == null || host == null)
				return;

			AnimatorPreviewerViewState s = host.GetViewState();
			AnimatorPreviewerTheme t = host.Theme;

			bool isClipMode = s.mode == AnimatorPreviewerMode.Clip;

			_playbackBlock.Refresh(
				playing: s.isPlaying && isClipMode,
				speed: s.timeScale,
				loop: s.loop,
				playAccent: t.accentClip,
				pauseAccent: t.pauseOrange);
		}

		private void InvalidateEventsCache()
		{
			_cachedClipId = 0;
			_cachedEventsTime = 0;
			_cachedClipEvents = Array.Empty<AnimationEvent>();
		}

		private AnimationEvent[] GetSafeCachedEvents(IAnimatorPreviewerHost host, AnimationClip clip)
		{
			if (host == null || clip == null)
				return Array.Empty<AnimationEvent>();

			int id = clip.GetInstanceID();
			double now = EditorApplication.timeSinceStartup;

			bool needRefresh = (id != _cachedClipId) || (now - _cachedEventsTime) > EVENTS_CACHE_REFRESH_SEC;

			if (!needRefresh)
				return _cachedClipEvents ?? Array.Empty<AnimationEvent>();

			_cachedClipId = id;
			_cachedEventsTime = now;

			_cachedClipEvents = host.GetClipEventsSafe(clip) ?? Array.Empty<AnimationEvent>();

			return _cachedClipEvents;
		}

		#endregion Private Methods
	}
}

