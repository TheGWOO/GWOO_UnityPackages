using System;
using GWOO.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    public class MaterialManagerWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "Material Manager";
        private const string UXML_PATH = "UIDocument/MaterialManager_EditorWindow";
        private const float ACTIONS_SPLIT_DEFAULT_WIDTH = 350f;
        private const float MAIN_PANEL_MIN_WIDTH = 350f;
        private const float ACTIONS_PEEK_MIN_WIDTH = 64f;
        private const float SPLIT_SIDE_MARGIN = 4f;
        private const float MIN_WINDOW_WIDTH = MAIN_PANEL_MIN_WIDTH + ACTIONS_PEEK_MIN_WIDTH;
        private const float MIN_WINDOW_HEIGHT = 420f;
        private static readonly Color ACCENT_COLOR = new(0.5f, 0.7f, 0.9f, 1f);

        private MaterialManagerController _controller;

        private VisualElement _topToolbarHost;
        private ToolbarToggle _showActionsToggle;

        private MaterialManagerQueryBlock _queryBlock;
        private TwoPaneSplitView _mainPanels;
        private VisualElement _leftPanel;
        private Card _filterCard;
        private Card _resultsCard;
        private Card _actionsCard;
        private VisualElement _filterPanel;
        private SearchBarBlock _materialsSearchBar;
        private Toggle _showVariantsToggle;
        private Label _showVariantsLabel;
        private CustomButton _propertyFilterButton;
        private CustomButton _clearFilterButton;
        private ScrollView _actionsScroll;
        private MaterialManagerResultsBlock _resultsBlock;
        private MaterialManagerActionsBlock _actionsBlock;
        private MaterialManagerStatusBlock _statusBlock;
        private bool _pendingActionsVisible;
        private bool _createGuiScheduled;

        [MenuItem("Tools/Material Manager %m", false, 1)]
        public static void OpenEditorWindow()
        {
            MaterialManagerWindow window = GetWindow<MaterialManagerWindow>();
            window.titleContent = new GUIContent(WINDOW_TITLE);
            window.minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        }

        private void CreateGUI()
        {
            if (EditorApplication.isUpdating)
            {
                ScheduleCreateGui();
                return;
            }

            _createGuiScheduled = false;

            DisposeController();

            rootVisualElement.Clear();

            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(UXML_PATH);
            if (visualTree != null)
            {
                visualTree.CloneTree(rootVisualElement);
            }

            MaterialManagerUIFactory.ApplyRootThemeAndStyles(rootVisualElement);

            BuildBlocks();
            BuildController();
            RegisterUICallbacks();
            ApplyState();
        }

        private void OnDisable()
        {
            DisposeController();
        }

        private void BuildBlocks()
        {
            _topToolbarHost = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "top-toolbar-host");
            VisualElement queryBlockHost = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "query-block");
            _queryBlock = CreateBlock<MaterialManagerQueryBlock>(queryBlockHost);

            VisualElement mainPanelsPlaceholder = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "main-panels");
            _leftPanel = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "left-panel");
            _filterCard = MaterialManagerUIFactory.RequireElement<Card>(rootVisualElement, "filter-card");
            _resultsCard = MaterialManagerUIFactory.RequireElement<Card>(rootVisualElement, "results-card");
            _actionsCard = MaterialManagerUIFactory.RequireElement<Card>(rootVisualElement, "actions-card");
            _filterPanel = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "filter-panel");
            VisualElement materialsSearchBarHost = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "materials-search-bar");
            _materialsSearchBar = CreateBlock<SearchBarBlock>(materialsSearchBarHost);
            _showVariantsToggle = MaterialManagerUIFactory.RequireElement<Toggle>(rootVisualElement, "show-variants-toggle");
            _showVariantsLabel = MaterialManagerUIFactory.RequireElement<Label>(rootVisualElement, "show-variants-label");
            _propertyFilterButton = MaterialManagerUIFactory.RequireElement<CustomButton>(rootVisualElement, "property-filter-button");
            _clearFilterButton = MaterialManagerUIFactory.RequireElement<CustomButton>(rootVisualElement, "clear-filter-button");
            _actionsScroll = MaterialManagerUIFactory.RequireElement<ScrollView>(rootVisualElement, "actions-scroll");
            _actionsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            VisualElement resultsBlockHost = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "results-block");
            _resultsBlock = CreateBlock<MaterialManagerResultsBlock>(resultsBlockHost);

            VisualElement actionsBlockHost = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "actions-block");
            _actionsBlock = CreateBlock<MaterialManagerActionsBlock>(actionsBlockHost);

            VisualElement statusBlockHost = MaterialManagerUIFactory.RequireElement<VisualElement>(rootVisualElement, "status-block");
            _statusBlock = CreateBlock<MaterialManagerStatusBlock>(statusBlockHost);

            ApplyCardAccentColors();

            BuildSplitView(mainPanelsPlaceholder);
            _mainPanels.RegisterCallback<GeometryChangedEvent>(OnSplitGeometryChanged);

            BuildLayoutToolbar();

            _materialsSearchBar.Configure(
                "Search materials...",
                "Search materials by name or tags. Use -tag to exclude.");

            _showVariantsToggle.tooltip = "Show or hide variant descendants that are not direct children of the source root material.";
            _showVariantsLabel.tooltip = _showVariantsToggle.tooltip;
            _propertyFilterButton.tooltip = "Filter the current result list by overridden source shader property.";
            _clearFilterButton.tooltip = "Remove the current property override filter.";
        }

        private void BuildController()
        {
            _controller = new MaterialManagerController();
            _controller.StateChanged += ApplyState;
            _controller.Initialize();
        }

        private void RegisterUICallbacks()
        {
            _queryBlock.ScopeChanged += _controller.SetSearchScope;
            _queryBlock.BrowseFolderClicked += BrowseFolder;
            _queryBlock.DefaultFolderClicked += () => _controller.SetFolderPath(MaterialManagerState.DEFAULT_FOLDER_PATH);
            _queryBlock.SourceShaderChanged += _controller.SetSourceShader;
            _queryBlock.FindClicked += _controller.FindMaterials;

            _materialsSearchBar.QueryChanged += _controller.SetSearchQuery;
            _showVariantsToggle.RegisterValueChangedCallback(evt => _controller.SetShowVariants(evt.newValue));
            _showVariantsLabel.RegisterCallback<MouseDownEvent>(_ => _showVariantsToggle.value = !_showVariantsToggle.value);
            _propertyFilterButton.clicked += OpenPropertyFilterWindow;
            _clearFilterButton.clicked += _controller.ClearPropertyFilter;

            _showActionsToggle.RegisterValueChangedCallback(evt => _controller.SetActionsPanelVisible(evt.newValue));

            _resultsBlock.MaterialIncludeChanged += _controller.SetMaterialIncluded;
            _resultsBlock.SelectAllClicked += _controller.SelectAllMaterials;
            _resultsBlock.SelectVisibleClicked += _controller.SelectVisibleMaterials;

            _actionsBlock.RevertClicked += OnRevertClicked;
            _actionsBlock.RevertVisibleOnlyChanged += _controller.SetRevertVisibleOnly;
            _actionsBlock.TargetShaderChanged += _controller.SetTargetShader;
            _actionsBlock.RebindClicked += OpenPropertyRebindWindow;
            _actionsBlock.ReparentClicked += OnReparentClicked;
            _actionsBlock.ReparentVisibleOnlyChanged += _controller.SetReparentVisibleOnly;
        }

        private void ApplyState()
        {
            if (_controller == null)
            {
                return;
            }

            MaterialManagerState state = _controller.State;

            _queryBlock.SetScope(state.searchScope);
            _queryBlock.SetFolderPath(state.folderPath);
            _queryBlock.SetSourceShader(state.sourceShader);
            _queryBlock.SetFindEnabled(state.sourceShader != null);
            _pendingActionsVisible = state.actionsPanelVisible;
            ApplyActionsPanelVisibility(state.actionsPanelVisible);
            UpdateLayoutControls(state.actionsPanelVisible);

            _materialsSearchBar.SetQuery(state.searchQuery);
            _showVariantsToggle.SetValueWithoutNotify(state.showVariants);
            _filterPanel.SetEnabled(state.foundMaterials.Count > 0);

            bool hasPropertyFilter = !string.IsNullOrEmpty(state.filterPropertyDisplayName);
            _propertyFilterButton.text = hasPropertyFilter
                ? state.filterPropertyDisplayName
                : "Filter by property override";
            _clearFilterButton.style.display = hasPropertyFilter
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            _resultsBlock.SetItems(state.visibleMaterials);
            _resultsBlock.SetSelectionActionsEnabled(state.foundMaterials.Count > 0);

            _actionsBlock.SetTargetShader(state.targetShader);
            _actionsBlock.SetRevertVisibleOnly(state.revertVisibleOnly);
            _actionsBlock.SetRevertEnabled(state.foundMaterials.Count > 0);
            _actionsBlock.SetRevertButtonText(state.filterPropertyDisplayName);

            bool showReparentControls = state.sourceShader != null && state.targetShader != null;
            _actionsBlock.SetReparentControlsDisplay(showReparentControls);
            _actionsBlock.SetReparentVisibleOnly(state.reparentVisibleOnly);
            _actionsBlock.SetRebindEnabled(showReparentControls);
            _actionsBlock.SetReparentEnabled(
                showReparentControls
                && state.foundMaterials.Count > 0);

            _statusBlock.SetStats(
                state.foundMaterials.Count,
                state.variantChildrenCount,
                state.materialsReparentedCount,
                state.propertiesRevertedCount);
            _statusBlock.SetActionSummary(state.actionSummaryMessage, state.actionSummaryType);
            _statusBlock.SetLog(
                state.lastLogMessage,
                state.lastLogType,
                state.lastLogDurationSeconds,
                state.lastLogSequence);
        }

        private void OnRevertClicked()
        {
            MaterialActionDryRunSummary summary = _controller.GetRevertDryRunSummary();

            bool shouldRun = EditorUtility.DisplayDialog(
                "Confirm Cleanup",
                BuildDryRunMessage(
                    "Cleanup will revert identical overrides on selected targets.",
                    summary),
                "Run Cleanup",
                "Cancel");

            if (!shouldRun)
            {
                return;
            }

            _controller.RevertIdenticalOverrides();
        }

        private void OnReparentClicked()
        {
            MaterialActionDryRunSummary summary = _controller.GetReparentDryRunSummary();

            bool shouldRun = EditorUtility.DisplayDialog(
                "Confirm Reparent",
                BuildDryRunMessage(
                    "Reparent will mutate material inheritance and then clean identical overrides.",
                    summary),
                "Run Reparent",
                "Cancel");

            if (!shouldRun)
            {
                return;
            }

            _controller.ReparentMaterials();
        }

        private void OpenPropertyFilterWindow()
        {
            MaterialManagerState state = _controller.State;
            if (state.sourceShader == null)
            {
                return;
            }

            ShaderPropertyFilterWindow.ShowWindow(
                state.sourceShader,
                state.showAllProperties,
                _controller.SetPropertyFilter,
                _controller.SetShowAllProperties,
                state.filterPropertyName);
        }

        private void OpenPropertyRebindWindow()
        {
            MaterialManagerState state = _controller.State;
            if (state.sourceShader == null || state.targetShader == null)
            {
                return;
            }

            ShaderPropertyRebindWindow.ShowWindow(
                state.sourceShader,
                state.targetShader,
                _controller.GetRebindMapCopy(),
                state.showAllProperties,
                _controller.SetRebindMappings);
        }

        private void BrowseFolder()
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Search Folder", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            string normalizedSelectedPath = selectedPath.Replace('\\', '/');
            string normalizedDataPath = Application.dataPath.Replace('\\', '/');

            if (!normalizedSelectedPath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                _controller.LogExternalFolderRejected();
                return;
            }

            string relativePath = normalizedSelectedPath.Length == normalizedDataPath.Length
                ? MaterialManagerState.DEFAULT_FOLDER_PATH
                : $"Assets{normalizedSelectedPath[normalizedDataPath.Length..]}";

            _controller.SetFolderPath(relativePath);
        }

        private void DisposeController()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.StateChanged -= ApplyState;
            _controller.Dispose();
            _controller = null;
        }

        private void BuildSplitView(VisualElement placeholder)
        {
            if (placeholder?.parent == null)
            {
                throw new InvalidOperationException("Main panels placeholder is missing a parent container.");
            }

            VisualElement parent = placeholder.parent;
            int index = parent.IndexOf(placeholder);

            _mainPanels = new TwoPaneSplitView(
                1,
                ACTIONS_SPLIT_DEFAULT_WIDTH,
                TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "mm-main-panels-split",
                style =
                {
                    flexGrow = 1f,
                    minWidth = 0,
                    minHeight = 0,
                }
            };
            _mainPanels.AddToClassList("mm-main-panels");

            _leftPanel.style.minWidth = MAIN_PANEL_MIN_WIDTH;
            _leftPanel.style.flexGrow = 1f;
            _leftPanel.style.minHeight = 0;

            _actionsScroll.style.minWidth = ACTIONS_PEEK_MIN_WIDTH;
            _actionsScroll.style.minHeight = 0;
            _actionsScroll.style.overflow = Overflow.Hidden;

            _mainPanels.Add(_leftPanel);
            _mainPanels.Add(_actionsScroll);

            placeholder.RemoveFromHierarchy();
            parent.Insert(index, _mainPanels);

            _mainPanels.CollapseChild(1);
            _pendingActionsVisible = false;
        }

        private void ApplyActionsPanelVisibility(bool showActions)
        {
            _leftPanel.style.marginRight = showActions ? SPLIT_SIDE_MARGIN : 0f;
            _actionsScroll.style.marginLeft = showActions ? SPLIT_SIDE_MARGIN : 0f;

            if (_mainPanels == null)
            {
                return;
            }

            if (showActions)
            {
                _mainPanels.UnCollapse();
            }
            else
            {
                _mainPanels.CollapseChild(1);
            }
        }

        private void OnSplitGeometryChanged(GeometryChangedEvent evt)
        {
            if (_mainPanels == null || evt.newRect.width <= 0f || evt.newRect.height <= 0f)
            {
                return;
            }

            ApplyActionsPanelVisibility(_pendingActionsVisible);
        }

        private void ApplyCardAccentColors()
        {
            if (_filterCard != null) _filterCard.AccentColor = ACCENT_COLOR;
            if (_resultsCard != null) _resultsCard.AccentColor = ACCENT_COLOR;
            if (_actionsCard != null) _actionsCard.AccentColor = ACCENT_COLOR;
        }

        private void UpdateLayoutControls(bool showActions)
        {
            _showActionsToggle.SetValueWithoutNotify(showActions);
            _showActionsToggle.tooltip = "Show or hide the actions side panel.";
        }

        private void BuildLayoutToolbar()
        {
            _topToolbarHost.Clear();

            Toolbar toolbar = new();
            toolbar.AddToClassList("mm-layout-toolbar");
            _topToolbarHost.Add(toolbar);

            VisualElement grow = new();
            grow.style.flexGrow = 1f;
            toolbar.Add(grow);

            _showActionsToggle = new ToolbarToggle
            {
                text = "Actions",
                tooltip = "Show or hide the actions side panel.",
            };
            _showActionsToggle.AddToClassList("mm-layout-toolbar-toggle");
            toolbar.Add(_showActionsToggle);
        }

        private static string BuildDryRunMessage(string intro, MaterialActionDryRunSummary summary)
        {
            return
                $"{intro}\n\n"
                + "Dry-run summary:\n"
                + $"- Materials in scope: {summary.TargetMaterialsCount}\n"
                + $"- Revertible overrides: {summary.RevertableOverridesCount}\n"
                + $"- Reparent candidates: {summary.ReparentCandidatesCount}\n\n"
                + "Proceed?";
        }

        private static T CreateBlock<T>(VisualElement host) where T : VisualElement, new()
        {
            if (host == null)
            {
                throw new InvalidOperationException($"Cannot create block '{typeof(T).Name}' because the host container is null.");
            }

            if (host.parent == null)
            {
                throw new InvalidOperationException($"Cannot create block '{typeof(T).Name}' because the host container has no parent.");
            }

            T block = new();

            if (!string.IsNullOrEmpty(host.name))
            {
                block.name = host.name;
            }

            foreach (string className in host.GetClasses())
            {
                block.AddToClassList(className);
            }

            VisualElement parent = host.parent;
            int hostIndex = parent.IndexOf(host);
            parent.Insert(hostIndex, block);
            host.RemoveFromHierarchy();

            return block;
        }

        private void ScheduleCreateGui()
        {
            if (_createGuiScheduled)
            {
                return;
            }

            _createGuiScheduled = true;
            EditorApplication.delayCall += TryCreateGuiAfterUpdate;
        }

        private void TryCreateGuiAfterUpdate()
        {
            if (this == null)
            {
                return;
            }

            if (EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryCreateGuiAfterUpdate;
                return;
            }

            _createGuiScheduled = false;
            CreateGUI();
        }

    }
}
