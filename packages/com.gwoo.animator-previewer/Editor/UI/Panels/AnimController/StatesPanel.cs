using System.Collections.Generic;
using GWOO.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class StatesPanel : IAnimatorPreviewerPanel
	{
		public VisualElement Root { get; private set; }

		private Card _card;

		private Label _countLabel;
		private ToolbarToggle _showLayerToggle;
		private ToolbarToggle _showPathToggle;
		private CustomButton _refreshButton;
		private ToolbarSearchField _searchField;

		private ListView _listView;

		private readonly List<AnimatorPreviewerStateEntry> _items = new();
		private readonly CallbackScope _callbacks = new();

		private bool _built;
		private IAnimatorPreviewerHost _host;

		private int _lastVisibleCount;

		private AnimatorPreviewerViewState _cachedViewState;
		private AnimatorPreviewerTheme _cachedTheme;

		public void Build(VisualElement parent, IAnimatorPreviewerHost host)
		{
			Dispose();

			_host = host;

			Color accent = host.Theme.accentClip;

			_card = new Card("States", accent);
			_card.style.flexGrow = 1f;
			_card.style.minHeight = 220;

			// Grab header bits if we need to modify accent color later
			// The card styling automatically handles accent color changes if we update AccentColor property
			// but we stored _accentStrip in previous implementation to change its color.
			// With new Card, we should just set _card.AccentColor.
			// But for now, let's just use the Card.
			
			VisualElement headerRow = _card.Header;
			headerRow.style.minHeight = 25f;

			_countLabel = new Label
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					opacity = 0.75f,
					marginRight = 6
				}
			};
			headerRow.Add(_countLabel);

			_showLayerToggle = headerRow.CreateAndBind<ToolbarToggle, bool>(
				new ToolbarToggle { text = "Layer", tooltip = "Show layer pill tag in each row." },
				OnShowLayerChanged,
				_callbacks);

			_showPathToggle = headerRow.CreateAndBind<ToolbarToggle, bool>(
				new ToolbarToggle { text = "Path", tooltip = "Show full state path (Layer/SubSM/State) instead of only the leaf name." },
				OnShowPathChanged,
				_callbacks);

			headerRow.Add(PanelCard.Spacer(6, horizontal: true));

			_refreshButton = PanelCard.NewInlineButton("Refresh", () => _host?.CmdRefreshStates(), minWidth: 28);
			_refreshButton.tooltip = "Refresh states cache.";
			_refreshButton.style.marginRight = 0;
			headerRow.Add(_refreshButton);

			VisualElement searchRow = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					marginBottom = 8
				}
			};
			_card.Add(searchRow);

			_searchField = new ToolbarSearchField
			{
				style =
				{
					flexGrow = 1f,
					flexShrink = 1f
				}
			};

			Button clearButton = _searchField.Q<Button>("unity-cancel");
			if (clearButton != null)
			{
				clearButton.text = "clear";
				clearButton.style.fontSize = 11;
				clearButton.style.paddingBottom = 1;
				clearButton.style.width = 50;
			}

			searchRow.CreateAndBind<ToolbarSearchField, string>(
				_searchField,
				OnSearchChanged,
				_callbacks);

			_card.Add(new Separator(1));

			_listView = new ListView(_items, 40, () => new StateRowElement(host))
			{
				selectionType = SelectionType.Single,
				virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
				style =
				{
					flexGrow = 1f,
					flexShrink = 1f,
					minHeight = 140
				}
			};

			_listView.bindItem = (element, index) =>
			{
				if (element is not StateRowElement row) return;
				if (index < 0 || index >= _items.Count) return;

				row.Bind(_items[index], index, _cachedViewState, _cachedTheme);
			};

			_listView.selectionChanged += OnSelectionChanged;
			_callbacks.Add(() =>
			{
				if (_listView != null)
					_listView.selectionChanged -= OnSelectionChanged;
			});

			_card.Add(_listView);

			Root = _card;
			parent.Add(Root);

			_lastVisibleCount = 0;
			_built = true;
		}

		private void OnShowLayerChanged(bool newValue)
		{
			_host?.CmdSetStateDisplayFlag(AnimatorPreviewerStateDisplayFlags.ShowLayerTag, newValue);
		}

		private void OnShowPathChanged(bool newValue)
		{
			_host?.CmdSetStateDisplayFlag(AnimatorPreviewerStateDisplayFlags.ShowFullPath, newValue);
		}

		private void OnSearchChanged(string newValue)
		{
			_host?.CmdSetStateSearch(newValue ?? string.Empty);
		}

		private void OnSelectionChanged(IEnumerable<object> selection)
		{
			if (_host == null) return;

			foreach (object o in selection)
			{
				if (o is AnimatorPreviewerStateEntry s)
				{
					_host.CmdSetSelectedState(s.layerIndex, s.stateHash);
					break;
				}
			}
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			_host = host;

			if (!_built || Root == null)
				return;

			_cachedViewState = host.GetViewState();
			_cachedTheme = host.Theme;

			AnimatorPreviewerViewState vs = _cachedViewState;
			AnimatorPreviewerTheme t = _cachedTheme;

			_card.SetEnabled(vs.isBound);
			if (!vs.isBound)
				return;

			Color accent = (vs.mode == AnimatorPreviewerMode.Clip) ? t.accentClip : t.accentCtrl;
			_card.AccentColor = accent;

			_countLabel.text = vs.totalStateCount == 0 ? string.Empty : $"{vs.visibleStateCount}/{vs.totalStateCount}";

			_showLayerToggle.SetValueWithoutNotify((vs.stateDisplayFlags & AnimatorPreviewerStateDisplayFlags.ShowLayerTag) != 0);
			_showPathToggle.SetValueWithoutNotify((vs.stateDisplayFlags & AnimatorPreviewerStateDisplayFlags.ShowFullPath) != 0);
			_searchField.SetValueWithoutNotify(vs.stateSearch ?? string.Empty);

			_items.Clear();

			IReadOnlyList<AnimatorPreviewerStateEntry> src = host.GetVisibleStates();
			if (src != null)
			{
				for (int i = 0; i < src.Count; i++)
					_items.Add(src[i]);
			}

			int visibleCount = _items.Count;

			if (_lastVisibleCount != visibleCount)
			{
				_lastVisibleCount = visibleCount;
				_listView.Rebuild();
			}
			else
			{
				_listView.RefreshItems();
			}
		}

		public void Dispose()
		{
			_built = false;

			_callbacks.Clear();
			_items.Clear();

			this.SafelyRemovePanel();

			_listView = null;

			_searchField = null;
			_refreshButton = null;

			_showLayerToggle = null;
			_showPathToggle = null;
			_countLabel = null;

			_card = null;

			_lastVisibleCount = 0;

			_cachedViewState = default;
			_cachedTheme = null;

			Root = null;
			_host = null;
		}

		public void SetVisible(bool visible)
		{
			PanelCard.SetDisplay(Root, visible);
		}
	}
}

