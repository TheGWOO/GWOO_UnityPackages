using System;
using System.Collections.Generic;
using System.Linq;
using GWOO.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    public class ShaderPropertyFilterWindow : EditorWindow
    {
        private const string UXML_PATH = "UIDocument/ShaderPropertyFilterWindow";

        private readonly ShaderPropertyService _propertyService = new();

        private Shader _shader;
        private bool _showAllProperties;
        private string _pendingPropertyName;
        private string _pendingPropertyDisplayName;
        private string _searchQuery = string.Empty;

        private Action<string, string> _onSelect;
        private Action<bool> _onShowAllChanged;

        private SearchBarBlock _searchBar;
        private Label _selectionSummaryLabel;
        private ScrollView _propertiesScroll;

        public static void ShowWindow(
            Shader shader,
            bool showAllProperties,
            Action<string, string> onSelect,
            Action<bool> onShowAllChanged,
            string selectedPropertyName = null)
        {
            ShaderPropertyFilterWindow window = GetWindow<ShaderPropertyFilterWindow>(true, "Properties");
            window.minSize = new Vector2(300f, 360f);

            window._shader = shader;
            window._showAllProperties = showAllProperties;
            window._onSelect = onSelect;
            window._onShowAllChanged = onShowAllChanged;
            window._pendingPropertyName = selectedPropertyName;
            window._pendingPropertyDisplayName = selectedPropertyName;

            if (window.rootVisualElement.childCount > 0)
            {
                window.RefreshProperties();
                window.RefreshSelectionSummary();
            }

            window.Show();
        }

        private void CreateGUI()
        {
            VisualElement root = MaterialManagerUIFactory.BuildPopupRoot(rootVisualElement);

            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(UXML_PATH);
            if (visualTree == null)
            {
                throw new InvalidOperationException($"Missing popup UXML at '{UXML_PATH}'.");
            }

            visualTree.CloneTree(root);

            PopupHeaderBlock header = MaterialManagerUIFactory.RequireElement<PopupHeaderBlock>(root, "popup-header");
            _searchBar = MaterialManagerUIFactory.RequireElement<SearchBarBlock>(root, "popup-search-bar");
            _selectionSummaryLabel = MaterialManagerUIFactory.RequireElement<Label>(root, "selection-summary-label");
            _propertiesScroll = MaterialManagerUIFactory.RequireElement<ScrollView>(root, "properties-scroll");
            Button resetButton = MaterialManagerUIFactory.RequireElement<Button>(root, "reset-button");
            Button applyButton = MaterialManagerUIFactory.RequireElement<Button>(root, "apply-button");
            Button cancelButton = MaterialManagerUIFactory.RequireElement<Button>(root, "cancel-button");

            resetButton.tooltip = "Stage no property filter.";
            applyButton.tooltip = "Apply selected property filter and close.";
            cancelButton.tooltip = "Close without applying staged selection.";

            header.SetTitle("Select property to filter");
            header.SetOptionsTooltip("Filter options");
            header.OptionsClicked += ShowOptionsMenu;

            _searchBar.Configure("Search properties...", "Filter properties by name.");
            _searchBar.QueryChanged += OnSearchChanged;
            _searchBar.SetQuery(_searchQuery);

            resetButton.clicked += ResetSelection;
            applyButton.clicked += ApplySelection;
            cancelButton.clicked += Close;

            RefreshProperties();
            RefreshSelectionSummary();
        }

        private void OnSearchChanged(string query)
        {
            _searchQuery = query;
            RefreshProperties();
        }

        private void RefreshProperties()
        {
            if (_propertiesScroll == null)
            {
                return;
            }

            Vector2 previousOffset = _propertiesScroll.scrollOffset;

            _propertiesScroll.Clear();

            if (_shader == null)
            {
                _propertiesScroll.Add(new Label("Select a source shader first."));
                return;
            }

            List<ShaderPropertyDescriptor> properties = _propertyService.GetProperties(_shader, _showAllProperties);
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                properties = properties
                    .Where(property => ShaderPropertySearchUtils.MatchesSearch(property, _searchQuery))
                    .ToList();
            }

            if (properties.Count == 0)
            {
                _propertiesScroll.Add(new Label("No matching properties."));
                return;
            }

            foreach (ShaderPropertyDescriptor property in properties)
            {
                ShaderPropertyDescriptor localProperty = property;

                string displayName = string.IsNullOrEmpty(localProperty.DisplayName)
                    ? localProperty.Name
                    : localProperty.DisplayName;

                CustomButton button = new(() => OnPropertySelected(localProperty.Name, displayName))
                {
                    text = displayName,
                    focusable = false,
                    tooltip = localProperty.Name,
                    Width = 100,
                };

                if (localProperty.Name == _pendingPropertyName)
                {
                    button.AddToClassList("primary-color");
                }

                _propertiesScroll.Add(button);
            }

            _propertiesScroll.scrollOffset = previousOffset;
        }

        private void OnPropertySelected(string propertyName, string propertyDisplayName)
        {
            _pendingPropertyName = propertyName;
            _pendingPropertyDisplayName = propertyDisplayName;
            RefreshProperties();
            RefreshSelectionSummary();
        }

        private void ResetSelection()
        {
            _pendingPropertyName = null;
            _pendingPropertyDisplayName = null;
            RefreshProperties();
            RefreshSelectionSummary();
        }

        private void ApplySelection()
        {
            if (string.IsNullOrEmpty(_pendingPropertyName))
            {
                _onSelect?.Invoke(null, null);
            }
            else
            {
                string displayName = string.IsNullOrEmpty(_pendingPropertyDisplayName)
                    ? _pendingPropertyName
                    : _pendingPropertyDisplayName;

                _onSelect?.Invoke(_pendingPropertyName, displayName);
            }

            Close();
        }

        private void RefreshSelectionSummary()
        {
            if (_selectionSummaryLabel == null)
            {
                return;
            }

            string selection = string.IsNullOrEmpty(_pendingPropertyDisplayName)
                ? "None"
                : _pendingPropertyDisplayName;

            _selectionSummaryLabel.text = $"Selected filter: {selection}";
        }

        private void ShowOptionsMenu()
        {
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Show all properties"),
                _showAllProperties,
                () =>
                {
                    _showAllProperties = !_showAllProperties;
                    _onShowAllChanged?.Invoke(_showAllProperties);
                    RefreshProperties();
                });

            menu.ShowAsContext();
        }

    }
}
