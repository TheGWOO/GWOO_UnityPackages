using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class LayersPanel : IAnimatorPreviewerPanel
	{
		private Foldout _rootFoldout;
		public VisualElement Root => _rootFoldout;

		private VisualElement _container;

		private readonly List<Slider> _sliders = new();
		private readonly CallbackScope _sliderCallbacks = new();

		private int _lastControllerId;
		private int _lastLayerCount;

		private bool _built;
		private IAnimatorPreviewerHost _host;

		public void Build(VisualElement parent, IAnimatorPreviewerHost host)
		{
			Dispose();

			_host = host;

			_rootFoldout = new Foldout { text = "Layers (Controller)", value = false };
			parent.Add(_rootFoldout);

			_container = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					paddingLeft = 6,
					paddingRight = 6,
					paddingBottom = 6
				}
			};
			_rootFoldout.Add(_container);

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
				return;

			AnimatorController ctrl = host.GetActiveController();
			int ctrlId = ctrl ? ctrl.GetInstanceID() : 0;
			int layerCount = (ctrl != null && ctrl.layers != null) ? ctrl.layers.Length : 0;

			bool needRebuild =
				ctrlId != _lastControllerId
				|| layerCount != _lastLayerCount
				|| _sliders.Count != layerCount;

			if (needRebuild)
			{
				RebuildSliders(ctrl, ctrlId, layerCount);
			}

			bool controllerMode = s.mode == AnimatorPreviewerMode.Controller;
			_container.SetEnabled(controllerMode);

			for (int i = 0; i < _sliders.Count; i++)
			{
				if (host.TryGetLayerWeight(i, out float w))
				{
					_sliders[i].SetValueWithoutNotify(w);
				}
			}
		}

		private void RebuildSliders(AnimatorController ctrl, int ctrlId, int layerCount)
		{
			_container?.Clear();
			_sliders.Clear();
			_sliderCallbacks.Clear();

			if (ctrl == null || layerCount <= 0)
			{
				_container?.Add(new Label("No controller layers detected."));
				_lastControllerId = ctrlId;
				_lastLayerCount = layerCount;
				return;
			}

			for (int i = 0; i < layerCount; i++)
			{
				int layerIndex = i;

				string layerName = ctrl.layers != null && layerIndex >= 0 && layerIndex < ctrl.layers.Length
					? ctrl.layers[layerIndex].name
					: "(unknown)";

				Slider slider = _container.CreateAndBind<Slider, float>(
					new Slider($"{layerIndex}: {layerName}", 0f, 1f),
					v => _host?.CmdSetLayerWeight(layerIndex, v),
					_sliderCallbacks);

				_sliders.Add(slider);
			}

			_lastControllerId = ctrlId;
			_lastLayerCount = layerCount;
		}

		public void Dispose()
		{
			_built = false;

			_sliderCallbacks.Clear();
			_sliders.Clear();

			this.SafelyRemovePanel();

			_container = null;
			_rootFoldout = null;

			_host = null;

			_lastControllerId = 0;
			_lastLayerCount = 0;
		}

		public void SetVisible(bool visible)
		{
			PanelCard.SetDisplay(_rootFoldout, visible);
		}
	}
}

