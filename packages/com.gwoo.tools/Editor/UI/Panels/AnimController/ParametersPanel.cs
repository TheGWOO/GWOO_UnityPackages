using System.Collections.Generic;
using GWOO.UIElements;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class ParametersPanel : IAnimatorPreviewerPanel
	{
		private Foldout _rootFoldout;
		public VisualElement Root => _rootFoldout;

		private ScrollView _scroll;

		private readonly List<IParamBinder> _binders = new();
		private readonly CallbackScope _paramCallbacks = new();

		private int _lastControllerId;

		private bool _built;
		private IAnimatorPreviewerHost _host;

		public void Build(VisualElement parent, IAnimatorPreviewerHost host)
		{
			Dispose();

			_host = host;

			_rootFoldout = new Foldout { text = "Parameters (Controller)", value = false };
			parent.Add(_rootFoldout);

			_scroll = new ScrollView
			{
				style =
				{
					minHeight = 90,
					maxHeight = 260
				}
			};
			_rootFoldout.Add(_scroll);

			_built = true;
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			_host = host;

			if (!_built || _rootFoldout == null)
				return;

			AnimatorPreviewerViewState s = host.GetViewState();

			_rootFoldout.SetEnabled(s.isBound);

			if (!s.isBound)
			{
				_lastControllerId = 0;
				return;
			}

			AnimatorController ctrl = host.GetActiveController();
			int ctrlId = ctrl ? ctrl.GetInstanceID() : 0;

			if (ctrlId != _lastControllerId)
			{
				RebuildUI(ctrl);
				_lastControllerId = ctrlId;
			}

			bool controllerMode = s.mode == AnimatorPreviewerMode.Controller;
			_scroll.SetEnabled(controllerMode);

			RefreshValues(host);
		}

		private void RebuildUI(AnimatorController ctrl)
		{
			_scroll.Clear();
			_binders.Clear();
			_paramCallbacks.Clear();

			if (ctrl == null)
			{
				_scroll.Add(new Label("No controller."));
				return;
			}

			foreach (AnimatorControllerParameter p in ctrl.parameters)
			{
				int hash = p.nameHash;

				switch (p.type)
				{
					case AnimatorControllerParameterType.Float:
					{
						Slider slider = new(p.name, -10f, 10f);

						EventCallback<ChangeEvent<float>> cb = evt => _host?.CmdSetFloat(hash, evt.newValue);
						slider.RegisterValueChangedCallback(cb);
						_paramCallbacks.Add(() => slider.UnregisterValueChangedCallback(cb));

						_scroll.Add(slider);
						_binders.Add(new FloatBinder(hash, slider));
						break;
					}

					case AnimatorControllerParameterType.Int:
					{
						IntegerField f = new(p.name);

						EventCallback<ChangeEvent<int>> cb = evt => _host?.CmdSetInt(hash, evt.newValue);
						f.RegisterValueChangedCallback(cb);
						_paramCallbacks.Add(() => f.UnregisterValueChangedCallback(cb));

						_scroll.Add(f);
						_binders.Add(new IntBinder(hash, f));
						break;
					}

					case AnimatorControllerParameterType.Bool:
					{
						Toggle t = new(p.name);

						EventCallback<ChangeEvent<bool>> cb = evt => _host?.CmdSetBool(hash, evt.newValue);
						t.RegisterValueChangedCallback(cb);
						_paramCallbacks.Add(() => t.UnregisterValueChangedCallback(cb));

						_scroll.Add(t);
						_binders.Add(new BoolBinder(hash, t));
						break;
					}

					case AnimatorControllerParameterType.Trigger:
					{
						VisualElement row = new()
						{
							style =
							{
								flexDirection = FlexDirection.Row,
								alignItems = Align.Center,
								flexWrap = Wrap.Wrap,
								marginLeft = 3,
								marginBottom = 6,
								marginTop = 6
							}
						};

						Label l = new(p.name)
						{
							pickingMode = PickingMode.Ignore,
							style =
							{
								minWidth = 135,
								flexGrow = 0f
							}
						};
						row.Add(l);

						CustomButton fire = new(() => _host?.CmdSetTrigger(hash), "primary-color")
						{
							text = "Trigger",
							Width = 0,
							style =
							{
								minHeight = 20,
								minWidth = 70,
								marginLeft = 0,
								marginRight = 6
							}
						};

						CustomButton reset = new(() => _host?.CmdResetTrigger(hash), "secondary-color")
						{
							text = "Reset",
							Width = 0,
							style =
							{
								minHeight = 20,
								minWidth = 60,
								marginLeft = 0,
								marginRight = 0
							}
						};

						row.Add(fire);
						row.Add(reset);

						_scroll.Add(row);
						break;
					}
				}
			}
		}

		public void RefreshValues(IAnimatorPreviewerHost host)
		{
			for (int i = 0; i < _binders.Count; i++)
			{
				_binders[i].Refresh(host);
			}
		}

		public void Dispose()
		{
			_built = false;

			_paramCallbacks.Clear();
			_binders.Clear();

			this.SafelyRemovePanel();

			_scroll = null;
			_rootFoldout = null;

			_host = null;

			_lastControllerId = 0;
		}

		public void SetVisible(bool visible)
		{
			PanelCard.SetDisplay(_rootFoldout, visible);
		}
	}
}

