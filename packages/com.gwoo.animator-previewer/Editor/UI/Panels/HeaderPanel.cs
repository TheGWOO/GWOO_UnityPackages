using System;
using GWOO.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class HeaderPanel : IAnimatorPreviewerPanel
	{
		public VisualElement Root { get; private set; }

		private VisualElement _headerStrip;
		private Toolbar _toolbar;

		private ObjectField _animatorField;
		private CustomButton _bindButton;
		private CustomButton _focusButton;

		private EnumField _modeField;

		private ToolbarToggle _autoTargetToggle;
		private ToolbarToggle _lockPosToggle;
		private ToolbarToggle _lockRotToggle;

		private readonly CallbackScope _callbacks = new();
		private bool _ignoreModeCallback;

		private bool _built;
		private IAnimatorPreviewerHost _host;

		public void Build(VisualElement parent, IAnimatorPreviewerHost host)
		{
			Dispose();

			_host = host;

			Root = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					flexShrink = 0
				}
			};

			_headerStrip = new VisualElement
			{
				style =
				{
					height = 3,
					flexShrink = 0
				}
			};
			Root.Add(_headerStrip);

			_toolbar = new Toolbar();
			Root.Add(_toolbar);

			BuildLeft(_toolbar);
			_toolbar.Add(new VisualElement { style = { flexGrow = 1f } });
			BuildRight(_toolbar);

			parent.Add(Root);
			_built = true;
		}

		private void BuildLeft(Toolbar toolbar)
		{
			VisualElement left = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexShrink = 0
				}
			};
			toolbar.Add(left);

			_animatorField = new ObjectField
			{
				objectType = typeof(Animator),
				allowSceneObjects = true,
				style =
				{
					width = 300,
					minWidth = 220,
					maxWidth = 420,
					flexGrow = 0,
					flexShrink = 1
				}
			};
			
			EventCallback<ChangeEvent<UnityEngine.Object>> animatorCb = OnAnimatorChanged;
			_animatorField.RegisterValueChangedCallback(animatorCb);
			_callbacks.Add(() => _animatorField.UnregisterValueChangedCallback(animatorCb));

			left.Add(_animatorField);

			_bindButton = PanelCard.NewToolbarButton("Bind", () => _host?.CmdToggleBind(), minWidth: 64);
			left.Add(_bindButton);

			_focusButton = PanelCard.NewToolbarButton("Focus", () => _host?.CmdFocusSceneView(), minWidth: 56);
			_focusButton.style.marginRight = 8;
			left.Add(_focusButton);
		}

		private void BuildRight(Toolbar toolbar)
		{
			VisualElement right = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexShrink = 0
				}
			};
			toolbar.Add(right);

			_modeField = new EnumField(AnimatorPreviewerMode.Clip)
			{
				style =
				{
					minWidth = 140,
					maxWidth = 190,
					flexShrink = 0
				}
			};

			_modeField.ElementAt(0).ElementAt(0).style.unityTextAlign =
				new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);

			EventCallback<ChangeEvent<Enum>> modeCb = OnModeChanged;
			_modeField.RegisterValueChangedCallback(modeCb);
			_callbacks.Add(() => _modeField.UnregisterValueChangedCallback(modeCb));

			right.Add(_modeField);
			right.Add(PanelCard.Spacer(8, horizontal: true));

			_autoTargetToggle = new ToolbarToggle
			{
				text = "Auto",
				tooltip = "Toggles automatic Animator target acquisition."
			};
			EventCallback<ChangeEvent<bool>> autoCb = evt => _host?.CmdSetAutoBind(evt.newValue);
			_autoTargetToggle.RegisterValueChangedCallback(autoCb);
			_callbacks.Add(() => _autoTargetToggle.UnregisterValueChangedCallback(autoCb));
			right.Add(_autoTargetToggle);

			_lockPosToggle = new ToolbarToggle
			{
				text = "Lock Pos",
				tooltip = "Lock root position to its current world position while previewing."
			};
			EventCallback<ChangeEvent<bool>> lockPosCb = evt => _host?.CmdSetLockPos(evt.newValue);
			_lockPosToggle.RegisterValueChangedCallback(lockPosCb);
			_callbacks.Add(() => _lockPosToggle.UnregisterValueChangedCallback(lockPosCb));
			right.Add(_lockPosToggle);

			_lockRotToggle = new ToolbarToggle
			{
				text = "Lock Rot",
				tooltip = "Lock root rotation to its current world rotation while previewing."
			};
			EventCallback<ChangeEvent<bool>> lockRotCb = evt => _host?.CmdSetLockRot(evt.newValue);
			_lockRotToggle.RegisterValueChangedCallback(lockRotCb);
			_callbacks.Add(() => _lockRotToggle.UnregisterValueChangedCallback(lockRotCb));
			right.Add(_lockRotToggle);
		}

		private void OnAnimatorChanged(ChangeEvent<UnityEngine.Object> evt)
		{
			_host?.CmdSetTargetAnimator(evt.newValue as Animator);
		}

		private void OnModeChanged(ChangeEvent<Enum> evt)
		{
			if (_ignoreModeCallback || _host == null)
				return;

			if (evt?.newValue is not AnimatorPreviewerMode next)
				return;

			AnimatorPreviewerMode prev = _host.GetViewState().mode;

			if (_host.CmdSetMode(next))
				return;

			_ignoreModeCallback = true;
			_modeField.SetValueWithoutNotify(prev);
			_ignoreModeCallback = false;
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			_host = host;

			if (!_built || Root == null)
				return;

			AnimatorPreviewerViewState s = host.GetViewState();
			AnimatorPreviewerTheme t = host.Theme;

			_animatorField?.SetValueWithoutNotify(s.targetAnimator);

			_bindButton.text = s.isBound ? "Unbind" : "Bind";
			_bindButton.SetEnabled(s.targetAnimator != null);

			_focusButton.SetEnabled(s.targetAnimator != null);

			PanelCard.SetClass(_bindButton, "primary-color", !s.isBound);
			PanelCard.SetClass(_bindButton, "secondary-color", s.isBound);

			_modeField.SetValueWithoutNotify(s.mode);

			_autoTargetToggle.SetValueWithoutNotify(s.autoBindToSelection);
			_lockPosToggle.SetValueWithoutNotify(s.lockRootPosition);
			_lockRotToggle.SetValueWithoutNotify(s.lockRootRotation);

			Color accent = (s.mode == AnimatorPreviewerMode.Clip) ? t.accentClip : t.accentCtrl;
			_headerStrip.style.backgroundColor = new StyleColor(accent);
		}

		public void Dispose()
		{
			_built = false;

			_callbacks.Clear();

			this.SafelyRemovePanel();

			_lockRotToggle = null;
			_lockPosToggle = null;
			_autoTargetToggle = null;

			_modeField = null;

			_focusButton = null;
			_bindButton = null;
			_animatorField = null;

			_toolbar = null;
			_headerStrip = null;

			Root = null;
			_host = null;
		}

		public void SetVisible(bool visible)
		{
			PanelCard.SetDisplay(Root, visible);
		}
	}
}

