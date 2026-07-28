using System;
using System.Collections.Generic;
using GWOO.UIElements;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class ControllerPreviewPanel : IAnimatorPreviewerPanel
	{
		#region Properties

		public VisualElement Root { get; private set; }

		#endregion Properties

		#region Fields

		private VisualElement _panel;

		private ScrollView _scroll;
		private ObjectField _controllerOverrideField;
		private Toggle _autoRebindToggle;

		private EventsBlock _eventsBlock;

		private Foldout _contextFoldout;
		private VisualElement _contextContainer;

		private readonly List<ControllerContextLayerUI> _layerUIs = new();
		private readonly List<AnimatorPreviewerControllerLayerContext> _contextScratch = new();

		private PlaybackBlock _playbackBlock;

		private readonly CallbackScope _callbacks = new();

		private bool _built;
		private IAnimatorPreviewerHost _host;

		private IVisualElementScheduledItem _pulseItem;
		private float _pulseStartTime;

		#endregion Fields

		#region Public Methods

		public void Build(VisualElement parent, IAnimatorPreviewerHost host)
		{
			Dispose();

			_host = host;

			AnimatorPreviewerTheme t = host.Theme;

			_panel = new Card("Controller Preview", t.accentCtrl);
			_panel.style.flexGrow = 1f;

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
			BuildContextGroup(_scroll);
			BuildPlaybackGroup(_scroll);

			Root = _panel;
			parent.Add(Root);

			_built = true;
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			_host = host;

			if (!_built || Root == null)
				return;

			AnimatorPreviewerViewState s = host.GetViewState();

			// Always update pulse here so it stops when the mode changes, even if context refresh isn't called.
			UpdatePulse(s);

			if (!s.isBound)
			{
				Root.SetEnabled(false);
				return;
			}

			Root.SetEnabled(true);

			AnimatorPreviewerTheme t = host.Theme;

			_controllerOverrideField.SetValueWithoutNotify(s.controllerOverride);
			_autoRebindToggle.SetValueWithoutNotify(s.autoRebindOnAssetChanges);

			_eventsBlock.SetEnabledValue(s.eventsEnabled);
			_eventsBlock.SetControllerFields(s.eventClipWeightThreshold);
			_eventsBlock.SetLogValue(s.logFiredEvents);

			_playbackBlock.Refresh(
				playing: s.isPlaying && s.mode == AnimatorPreviewerMode.Controller,
				speed: s.timeScale,
				loop: false,
				playAccent: t.accentCtrl,
				pauseAccent: t.pauseOrange);
		}

		public void RefreshContextIfOpen(IAnimatorPreviewerHost host)
		{
			_host = host;

			if (_contextFoldout == null)
				return;

			if (!_contextFoldout.value)
			{
				StopPulse();
				return;
			}

			AnimatorPreviewerViewState s = host.GetViewState();
			UpdatePulse(s);

			if (!s.isBound || s.mode != AnimatorPreviewerMode.Controller)
				return;

			_contextScratch.Clear();
			host.QueryControllerContext(_contextScratch);

			EnsureLayerUIs(host, _contextScratch.Count);

			for (int i = 0; i < _contextScratch.Count; i++)
			{
				AnimatorPreviewerControllerLayerContext ctx = _contextScratch[i];
				_layerUIs[i].Update(host, ctx, OnPreviewClipRequested);
			}
		}

		public void Dispose()
		{
			_built = false;

			_callbacks.Clear();

			_contextScratch.Clear();
			_layerUIs.Clear();
			StopPulse();

			this.SafelyRemovePanel();

			_contextContainer = null;
			_contextFoldout = null;

			_playbackBlock = null;

			_eventsBlock = null;

			_autoRebindToggle = null;
			_controllerOverrideField = null;
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

			VisualElement settings = new Card("Preview Settings", t.accentCtrl, isGroup: true);
			parent.Add(settings);

			_controllerOverrideField = settings.CreateAndBind<ObjectField, UnityEngine.Object>(
				new ObjectField("Override (optional)") { objectType = typeof(AnimatorController), allowSceneObjects = false },
				v => _host?.CmdSetControllerOverride(v as AnimatorController),
				_callbacks);

			_autoRebindToggle = settings.CreateAndBind<Toggle, bool>(
				new Toggle("Auto Rebind on Asset Changes"),
				v => _host?.CmdSetAutoRebindOnAssetChanges(v),
				_callbacks);
		}

		private void BuildEventsGroup(VisualElement parent, IAnimatorPreviewerHost host)
		{
			AnimatorPreviewerTheme t = host.Theme;

			VisualElement events = new Card("Events (Previewer)", t.accentCtrl, isGroup: true);
			parent.Add(events);

			_eventsBlock = new EventsBlock(isClipMode: false);
			Action eventsChanged = OnEventsBlockChanged;
			_eventsBlock.OnChanged += eventsChanged;
			_callbacks.Add(() =>
			{
				if (_eventsBlock != null)
					_eventsBlock.OnChanged -= eventsChanged;
			});
			events.Add(_eventsBlock);
		}

		private void BuildContextGroup(VisualElement parent)
		{
			_contextFoldout = new Foldout
			{
				text = "Context (Current State / Transition / Clips)",
				value = true,
				style =
				{
					marginTop = 2,
					marginBottom = 6
				}
			};

			parent.CreateAndBind<Foldout, bool>(
				_contextFoldout,
				v => 
				{
					if (!v) StopPulse();
					else UpdatePulse(_host?.GetViewState() ?? default);
				},
				_callbacks);

			_contextContainer = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					marginLeft = 6
				}
			};
			_contextFoldout.Add(_contextContainer);
		}

		private void BuildPlaybackGroup(VisualElement parent)
		{
			AnimatorPreviewerTheme t = _host.Theme;

			VisualElement playbackGroup = new Card("Playback", t.accentCtrl, isGroup: true);
			parent.Add(playbackGroup);

			_playbackBlock = new PlaybackBlock(showLoop: false);

			Action playPauseCb = () => _host?.CmdTogglePlayPause();
			Action<float> speedCb = v => _host?.CmdSetTimeScale(v);

			_playbackBlock.OnPlayPause += playPauseCb;
			_playbackBlock.OnSpeedChanged += speedCb;

			_callbacks.Add(() =>
			{
				if (_playbackBlock == null)
					return;

				_playbackBlock.OnPlayPause -= playPauseCb;
				_playbackBlock.OnSpeedChanged -= speedCb;
			});

			playbackGroup.Add(_playbackBlock);
		}

		private void OnEventsBlockChanged()
		{
			if (_host == null || _eventsBlock == null)
				return;

			_host.CmdSetEventsEnabled(_eventsBlock.GetEnabledValue());
			_host.CmdSetEventClipWeightThreshold(_eventsBlock.GetThreshold());
			_host.CmdSetLogFiredEvents(_eventsBlock.GetLogValue());
		}

		internal void RefreshPlaybackOnly(IAnimatorPreviewerHost host)
		{
			if (_playbackBlock == null || host == null)
				return;

			AnimatorPreviewerViewState s = host.GetViewState();
			AnimatorPreviewerTheme t = host.Theme;

			bool isControllerMode = s.mode == AnimatorPreviewerMode.Controller;

			_playbackBlock.Refresh(
				playing: s.isPlaying && isControllerMode,
				speed: s.timeScale,
				loop: s.loop,
				playAccent: t.accentCtrl,
				pauseAccent: t.pauseOrange);
		}

		private void OnPreviewClipRequested(int layerIndex, int clipId, float cycles)
		{
			_host?.CmdPreviewContextClip(clipId, cycles);
		}

		private void EnsureLayerUIs(IAnimatorPreviewerHost host, int count)
		{
			if (_contextContainer == null)
				return;

			// If shrinking to 0: clear everything.
			if (count <= 0)
			{
				_contextContainer.Clear();
				_layerUIs.Clear();
				return;
			}

			// If exact match, nothing to do.
			if (_layerUIs.Count == count)
				return;

			_contextContainer.Clear();
			_layerUIs.Clear();

			AnimatorPreviewerTheme t = host.Theme;

			for (int i = 0; i < count; i++)
			{
				ControllerContextLayerUI ui = new(t);
				_contextContainer.Add(ui.Root);
				_layerUIs.Add(ui);
			}
		}

		// --- pulse ---

		private void UpdatePulse(AnimatorPreviewerViewState s)
		{
			bool shouldPulse =
				_contextFoldout != null
				&& _contextFoldout.value
				&& s.isBound
				&& s.mode == AnimatorPreviewerMode.Controller
				&& s.isPlaying;

			if (shouldPulse)
				StartPulse();
			else
				StopPulse();
		}

		private void StartPulse()
		{
			if (_pulseItem != null || _contextContainer == null)
				return;

			_pulseStartTime = Time.realtimeSinceStartup;

			_pulseItem = _contextContainer.schedule.Execute(PulseTick).Every(33);
		}

		private void StopPulse()
		{
			if (_pulseItem == null)
				return;

			_pulseItem.Pause();
			_pulseItem = null;

			SetAllProgressBarOpacity(0.90f);
		}

		private void PulseTick()
		{
			float t = Time.realtimeSinceStartup - _pulseStartTime;
			float s = 0.5f + 0.5f * Mathf.Sin(t * (2f * Mathf.PI) * 1.2f);

			float opacity = Mathf.Lerp(0.60f, 0.95f, s);
			SetAllProgressBarOpacity(opacity);
		}

		private void SetAllProgressBarOpacity(float opacity)
		{
			for (int i = 0; i < _layerUIs.Count; i++)
			{
				_layerUIs[i].SetPulseOpacity(opacity);
			}
		}

		#endregion Private Methods
	}
}

