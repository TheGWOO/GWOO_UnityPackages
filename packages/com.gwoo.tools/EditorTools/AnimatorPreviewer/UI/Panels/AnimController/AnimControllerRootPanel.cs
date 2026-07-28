using GWOO.UIElements;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class AnimControllerRootPanel : IAnimatorPreviewerPanel
	{
		public VisualElement Root { get; private set; }

		private readonly LayersPanel _layers = new();
		private readonly ParametersPanel _params = new();
		private readonly StatesPanel _states = new();

		private bool _built;

		public void Build(VisualElement parent, IAnimatorPreviewerHost host)
		{
			Dispose();

			Root = new VisualElement
			{
				style =
				{
					flexGrow = 1f,
					flexDirection = FlexDirection.Column,
					minWidth = 320,
					paddingLeft = 8,
					paddingRight = 8,
					paddingTop = 8,
					paddingBottom = 8
				}
			};

			_layers.Build(Root, host);
			Root.Add(new Separator(1));

			_params.Build(Root, host);
			Root.Add(new Separator(1));

			_states.Build(Root, host);

			parent.Add(Root);
			_built = true;
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			if (!_built || Root == null)
				return;

			_layers.Refresh(host);
			_params.Refresh(host);
			_states.Refresh(host);
		}

		public void RefreshParamsOnly(IAnimatorPreviewerHost host)
		{
			if (!_built || Root == null)
				return;

			_params.RefreshValues(host);
		}
		
		public void RefreshStatesOnly(IAnimatorPreviewerHost host)
		{
			if (!_built || Root == null)
				return;

			_states.Refresh(host);
		}

		public void Dispose()
		{
			_built = false;

			_layers.Dispose();
			_params.Dispose();
			_states.Dispose();

			this.SafelyRemovePanel();
			Root = null;
		}

		public void SetVisible(bool visible)
		{
			PanelCard.SetDisplay(Root, visible);
		}
	}
}
