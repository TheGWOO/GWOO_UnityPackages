using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    public class ShaderPropertyRebindWindow : EditorWindow
    {
        private const string UXML_PATH = "UIDocument/ShaderPropertyRebindWindow";
        private const string NONE_LABEL = "<None>";

        private readonly ShaderPropertyService _propertyService = new();

        private Shader _sourceShader;
        private Shader _targetShader;
        private Dictionary<string, string> _mapping;
        private Dictionary<string, string> _originalMapping;
        private Action<Dictionary<string, string>> _onApply;

        private bool _showAllProperties;
        private string _searchQuery = string.Empty;

        private SearchBarBlock _searchBar;
        private Label _summaryLabel;
        private ScrollView _scrollView;
        private VisualElement _rowsRoot;

        public static void ShowWindow(
            Shader sourceShader,
            Shader targetShader,
            Dictionary<string, string> currentMapping,
            bool showAllProperties,
            Action<Dictionary<string, string>> onApply)
        {
            ShaderPropertyRebindWindow window = GetWindow<ShaderPropertyRebindWindow>(true, "Properties Rebind");
            window.minSize = new Vector2(520f, 420f);

            window._sourceShader = sourceShader;
            window._targetShader = targetShader;
            window._showAllProperties = showAllProperties;
            window._mapping = currentMapping != null
                ? new Dictionary<string, string>(currentMapping)
                : new Dictionary<string, string>();
            window._originalMapping = currentMapping != null
                ? new Dictionary<string, string>(currentMapping)
                : new Dictionary<string, string>();
            window._onApply = onApply;

            if (window.rootVisualElement.childCount > 0)
            {
                window.RebuildRows();
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
            Button autoMapButton = MaterialManagerUIFactory.RequireElement<Button>(root, "auto-map-button");
            Button clearMappingsButton = MaterialManagerUIFactory.RequireElement<Button>(root, "clear-mappings-button");
            Button removeInvalidButton = MaterialManagerUIFactory.RequireElement<Button>(root, "remove-invalid-button");
            _summaryLabel = MaterialManagerUIFactory.RequireElement<Label>(root, "summary-label");
            _scrollView = MaterialManagerUIFactory.RequireElement<ScrollView>(root, "mapping-scroll");
            _rowsRoot = MaterialManagerUIFactory.RequireElement<VisualElement>(root, "rows-root");
            Button resetButton = MaterialManagerUIFactory.RequireElement<Button>(root, "reset-button");
            Button applyButton = MaterialManagerUIFactory.RequireElement<Button>(root, "apply-button");
            Button cancelButton = MaterialManagerUIFactory.RequireElement<Button>(root, "cancel-button");

            resetButton.tooltip = "Restore mapping to the state when this window was opened.";
            applyButton.tooltip = "Apply current mapping and close.";
            cancelButton.tooltip = "Close without applying mapping changes.";

            header.SetTitle("Rebind source properties to compatible target properties");
            header.SetOptionsTooltip("Options");
            header.OptionsClicked += ShowOptionsMenu;

            _searchBar.Configure("Search properties...", "Filter source properties by name.");
            _searchBar.QueryChanged += OnSearchChanged;
            _searchBar.SetQuery(_searchQuery);

            autoMapButton.clicked += AutoMapCompatible;
            clearMappingsButton.clicked += ClearMappings;
            removeInvalidButton.clicked += RemoveInvalidMappings;
            resetButton.clicked += ResetMappings;
            applyButton.clicked += ApplyAndClose;
            cancelButton.clicked += Close;

            RebuildRows();
        }

        private void OnSearchChanged(string query)
        {
            _searchQuery = query;
            RebuildRows();
        }

        private void RebuildRows()
        {
            if (_rowsRoot == null)
            {
                return;
            }

            Vector2 previousOffset = _scrollView?.scrollOffset ?? Vector2.zero;

            _rowsRoot.Clear();

            if (_sourceShader == null || _targetShader == null)
            {
                _summaryLabel.text = "Select source and target shaders first.";
                return;
            }

            List<ShaderPropertyDescriptor> sourceProperties = _propertyService.GetProperties(_sourceShader, _showAllProperties);
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                sourceProperties = sourceProperties
                    .Where(property => ShaderPropertySearchUtils.MatchesSearch(property, _searchQuery))
                    .ToList();
            }

            if (sourceProperties.Count == 0)
            {
                _summaryLabel.text = "No properties to map.";
                return;
            }

            Dictionary<ShaderPropertyType, List<string>> compatibleTargetsByType = BuildCompatibleTargetsByType(
                sourceProperties.Select(property => property.Type),
                _showAllProperties);

            int mappedCount = 0;
            foreach (ShaderPropertyDescriptor sourceProperty in sourceProperties)
            {
                VisualElement row = new();
                row.AddToClassList("mm-row");
                row.AddToClassList("mm-popup-row");

                string sourceLabelText = string.IsNullOrEmpty(sourceProperty.DisplayName)
                    ? sourceProperty.Name
                    : $"{sourceProperty.DisplayName} ({sourceProperty.Name})";

                Label sourcePropertyLabel = new(sourceLabelText)
                {
                    tooltip = sourceProperty.Name,
                };
                sourcePropertyLabel.AddToClassList("mm-popup-source");
                row.Add(sourcePropertyLabel);

                List<string> compatibleTargets = BuildPopupChoices(sourceProperty.Type, compatibleTargetsByType);

                string current = NONE_LABEL;
                if (_mapping.TryGetValue(sourceProperty.Name, out string mappedProperty)
                    && !string.IsNullOrEmpty(mappedProperty)
                    && compatibleTargets.Contains(mappedProperty))
                {
                    current = mappedProperty;
                }

                if (current != NONE_LABEL)
                {
                    mappedCount++;
                }

                PopupField<string> targetPopup = new(string.Empty, compatibleTargets, current)
                {
                    tooltip = "Select mapped target property.",
                };
                targetPopup.AddToClassList("mm-popup-target");

                string localSourceProperty = sourceProperty.Name;
                targetPopup.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == NONE_LABEL)
                    {
                        _mapping.Remove(localSourceProperty);
                    }
                    else
                    {
                        _mapping[localSourceProperty] = evt.newValue;
                    }

                    RebuildRows();
                });

                row.Add(targetPopup);

                if (current == NONE_LABEL)
                {
                    row.AddToClassList("mm-map-unmapped");
                }

                _rowsRoot.Add(row);
            }

            _summaryLabel.text = $"Mapped {mappedCount}/{sourceProperties.Count} properties";
            if (_scrollView != null)
            {
                _scrollView.scrollOffset = previousOffset;
            }
        }

        private void AutoMapCompatible()
        {
            if (_sourceShader == null || _targetShader == null)
            {
                return;
            }

            List<ShaderPropertyDescriptor> sourceProperties = _propertyService.GetProperties(_sourceShader, _showAllProperties);
            Dictionary<ShaderPropertyType, List<string>> compatibleTargetsByType = BuildCompatibleTargetsByType(
                sourceProperties.Select(property => property.Type),
                _showAllProperties);

            foreach (ShaderPropertyDescriptor sourceProperty in sourceProperties)
            {
                if (compatibleTargetsByType.TryGetValue(sourceProperty.Type, out List<string> compatibleTargets)
                    && compatibleTargets.Contains(sourceProperty.Name))
                {
                    _mapping[sourceProperty.Name] = sourceProperty.Name;
                }
            }

            RebuildRows();
        }

        private void ClearMappings()
        {
            _mapping.Clear();
            RebuildRows();
        }

        private void ResetMappings()
        {
            _mapping = _originalMapping != null
                ? new Dictionary<string, string>(_originalMapping)
                : new Dictionary<string, string>();

            RebuildRows();
        }

        private void RemoveInvalidMappings()
        {
            if (_sourceShader == null || _targetShader == null)
            {
                return;
            }

            Dictionary<string, ShaderPropertyType> sourceTypes = _propertyService
                .GetProperties(_sourceShader, true)
                .ToDictionary(property => property.Name, property => property.Type);
            Dictionary<ShaderPropertyType, List<string>> compatibleTargetsByType = BuildCompatibleTargetsByType(sourceTypes.Values, true);

            List<string> keys = _mapping.Keys.ToList();
            foreach (string sourcePropertyName in keys)
            {
                if (!sourceTypes.TryGetValue(sourcePropertyName, out ShaderPropertyType sourceType))
                {
                    _mapping.Remove(sourcePropertyName);
                    continue;
                }

                if (!compatibleTargetsByType.TryGetValue(sourceType, out List<string> compatibleTargets)
                    || !_mapping.TryGetValue(sourcePropertyName, out string targetPropertyName)
                    || string.IsNullOrEmpty(targetPropertyName)
                    || !compatibleTargets.Contains(targetPropertyName))
                {
                    _mapping.Remove(sourcePropertyName);
                }
            }

            RebuildRows();
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
                    RebuildRows();
                });
            menu.ShowAsContext();
        }

        private void ApplyAndClose()
        {
            Dictionary<string, string> cleanMapping = _mapping
                .Where(entry => !string.IsNullOrEmpty(entry.Key) && !string.IsNullOrEmpty(entry.Value))
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            _onApply?.Invoke(cleanMapping);
            Close();
        }

        private Dictionary<ShaderPropertyType, List<string>> BuildCompatibleTargetsByType(
            IEnumerable<ShaderPropertyType> propertyTypes,
            bool showAllProperties)
        {
            Dictionary<ShaderPropertyType, List<string>> compatibleTargetsByType = new();
            if (_targetShader == null)
            {
                return compatibleTargetsByType;
            }

            HashSet<ShaderPropertyType> uniqueTypes = new(propertyTypes);
            foreach (ShaderPropertyType propertyType in uniqueTypes)
            {
                compatibleTargetsByType[propertyType] = _propertyService.GetCompatibleTargetProperties(
                    _targetShader,
                    propertyType,
                    showAllProperties);
            }

            return compatibleTargetsByType;
        }

        private static List<string> BuildPopupChoices(
            ShaderPropertyType propertyType,
            IReadOnlyDictionary<ShaderPropertyType, List<string>> compatibleTargetsByType)
        {
            List<string> choices = new() { NONE_LABEL };
            if (compatibleTargetsByType.TryGetValue(propertyType, out List<string> compatibleTargets))
            {
                choices.AddRange(compatibleTargets);
            }

            return choices;
        }
    }
}
