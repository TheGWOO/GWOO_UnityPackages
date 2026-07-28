using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GWOO.Editor.Tools
{
    internal sealed class MaterialManagerController
    {
        private readonly MaterialManagerState _state = new();
        private readonly MaterialManagerSettingsService _settingsService = new();
        private readonly MaterialQueryService _queryService = new();
        private readonly MaterialFilterService _filterService = new();
        private readonly MaterialMutationService _mutationService = new();
        private readonly ShaderPropertyService _shaderPropertyService = new();

        public event Action StateChanged;

        public MaterialManagerState State => _state;

        public void Initialize()
        {
            _settingsService.Load(_state);
            BuildDefaultRebindMap();
            RefreshVisible();
            SetActionSummary("Ready. No action executed yet.", MaterialLogType.Info);
            Log("Ready.", MaterialLogType.Info, 0f);
            NotifyChanged();
        }

        public void Dispose()
        {
            _settingsService.Save(_state);
        }

        public void SetSearchScope(MaterialSearchScope scope)
        {
            if (_state.searchScope == scope)
            {
                return;
            }

            _state.searchScope = scope;
            NotifyChanged();
        }

        public void SetFolderPath(string folderPath)
        {
            string nextPath = string.IsNullOrWhiteSpace(folderPath)
                ? MaterialManagerState.DEFAULT_FOLDER_PATH
                : folderPath;

            _state.folderPath = nextPath;
            NotifyChanged();
        }

        public void SetSourceShader(Shader shader)
        {
            if (_state.sourceShader == shader)
            {
                return;
            }

            _state.sourceShader = shader;
            _state.filterPropertyName = null;
            _state.filterPropertyDisplayName = null;

            _state.foundMaterials.Clear();
            _state.visibleMaterials.Clear();
            _state.sourceRootMaterial = null;
            _state.variantChildrenCount = 0;

            BuildDefaultRebindMap();
            NotifyChanged();
        }

        public void SetTargetShader(Shader shader)
        {
            if (_state.targetShader == shader)
            {
                return;
            }

            _state.targetShader = shader;
            BuildDefaultRebindMap();
            NotifyChanged();
        }

        public void SetSearchQuery(string query)
        {
            _state.searchQuery = query ?? string.Empty;
            RefreshVisible();
            NotifyChanged();
        }

        public void SetShowVariants(bool show)
        {
            _state.showVariants = show;
            RefreshVisible();
            NotifyChanged();
        }

        public void SetRevertVisibleOnly(bool value)
        {
            _state.revertVisibleOnly = value;
            NotifyChanged();
        }

        public void SetReparentVisibleOnly(bool value)
        {
            _state.reparentVisibleOnly = value;
            NotifyChanged();
        }

        public void SetShowAllProperties(bool value)
        {
            _state.showAllProperties = value;
            NotifyChanged();
        }

        public void SetActionsPanelVisible(bool value)
        {
            if (_state.actionsPanelVisible == value)
            {
                return;
            }

            _state.actionsPanelVisible = value;
            NotifyChanged();
        }

        public void SetMaterialIncluded(MaterialListItem item, bool included)
        {
            if (item == null)
            {
                return;
            }

            item.Included = included;
            NotifyChanged();
        }

        public void SetPropertyFilter(string propertyName, string propertyDisplayName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                ClearPropertyFilter();
                return;
            }

            _state.filterPropertyName = propertyName;
            _state.filterPropertyDisplayName = propertyDisplayName;
            RefreshVisible();
            Log($"Filtering {propertyDisplayName} overrides.", MaterialLogType.Info, 2f);
            NotifyChanged();
        }

        public void ClearPropertyFilter()
        {
            bool hadFilter = !string.IsNullOrEmpty(_state.filterPropertyName);
            _state.filterPropertyName = null;
            _state.filterPropertyDisplayName = null;
            RefreshVisible();
            if (hadFilter)
            {
                Log("Property override filter has been cleared.", MaterialLogType.Info, 1f);
            }
            NotifyChanged();
        }

        public void FindMaterials()
        {
            if (_state.sourceShader == null)
            {
                Log("Select a source shader first.", MaterialLogType.Error, 2f);
                SetActionSummary("Cannot run query: missing source shader.", MaterialLogType.Error);
                NotifyChanged();
                return;
            }

            FindMaterialsInternal();

            if (_state.foundMaterials.Count > 0)
            {
                Log("Materials found!", MaterialLogType.Success, 2f);
                SetActionSummary($"Query completed. {_state.foundMaterials.Count} materials match the source shader.", MaterialLogType.Success);
            }
            else
            {
                Log("No material found.", MaterialLogType.Error, 2f);
                SetActionSummary("Query completed. No material matches the current criteria.", MaterialLogType.Info);
            }

            NotifyChanged();
        }

        public void SelectAllMaterials()
        {
            Material[] materials = _state.foundMaterials
                .Where(item => item.Material != null)
                .Select(item => item.Material)
                .ToArray();

            Selection.objects = materials;

            if (materials.Length > 0)
            {
                Log($"{materials.Length} materials selected!", MaterialLogType.Success, 2f);
            }
            else
            {
                Log("No materials selected.", MaterialLogType.Error, 2f);
            }

            NotifyChanged();
        }

        public void SelectVisibleMaterials()
        {
            Material[] materials = _state.visibleMaterials
                .Where(item => item.Included && item.Material != null)
                .Select(item => item.Material)
                .ToArray();

            Selection.objects = materials;

            if (materials.Length > 0)
            {
                Log($"{materials.Length} materials selected!", MaterialLogType.Success, 2f);
            }
            else
            {
                Log("No materials selected.", MaterialLogType.Error, 2f);
            }

            NotifyChanged();
        }

        public void RevertIdenticalOverrides()
        {
            IReadOnlyList<MaterialListItem> targets = GetActionTargets(_state.revertVisibleOnly);
            int revertedCount = _mutationService.RevertIdenticalOverrides(targets, _state.filterPropertyName);
            _state.propertiesRevertedCount = revertedCount;

            if (revertedCount > 0)
            {
                Log($"{revertedCount} overrides have been cleaned up!", MaterialLogType.Success, 2f);
                SetActionSummary(
                    $"Cleanup completed on {targets.Count} materials. Reverted {revertedCount} identical overrides.",
                    MaterialLogType.Success);
            }
            else
            {
                Log("No useless overrides have been found!", MaterialLogType.Info, 2f);
                SetActionSummary(
                    $"Cleanup checked {targets.Count} materials. No identical override to revert.",
                    MaterialLogType.Info);
            }

            RefreshVisible();
            NotifyChanged();
        }

        public void ReparentMaterials()
        {
            if (_state.sourceShader == null || _state.targetShader == null)
            {
                Log("Select source and target shaders before reparenting.", MaterialLogType.Error, 2f);
                SetActionSummary("Cannot reparent: source or target shader is missing.", MaterialLogType.Error);
                NotifyChanged();
                return;
            }

            IReadOnlyList<MaterialListItem> targets = GetActionTargets(_state.reparentVisibleOnly);
            if (targets.Count == 0)
            {
                Log("No materials available for reparenting.", MaterialLogType.Error, 2f);
                SetActionSummary("Cannot reparent: there is no eligible material target.", MaterialLogType.Info);
                NotifyChanged();
                return;
            }

            MaterialMutationResult result = _mutationService.ReparentMaterials(
                _state.sourceShader,
                _state.sourceRootMaterial,
                _state.targetShader,
                _state.rebindMap,
                targets);

            _state.materialsReparentedCount = result.ReparentedCount;
            _state.propertiesRevertedCount = result.RevertedOverrideCount;

            FindMaterialsInternal();

            if (result.ReparentedCount > 0)
            {
                Log(
                    $"Reparented {result.ReparentedCount} materials. Cleaned {result.RevertedOverrideCount} overrides.",
                    MaterialLogType.Success,
                    5f);
                SetActionSummary(
                    $"Reparent completed. Reparented {result.ReparentedCount} candidates and cleaned {result.RevertedOverrideCount} overrides.",
                    MaterialLogType.Success);
            }
            else
            {
                Log("No material has been reparented.", MaterialLogType.Error, 5f);
                SetActionSummary("Reparent skipped. No candidate material could be reparented.", MaterialLogType.Error);
            }

            NotifyChanged();
        }

        public Dictionary<string, string> GetRebindMapCopy()
        {
            return new Dictionary<string, string>(_state.rebindMap);
        }

        public void SetRebindMappings(Dictionary<string, string> mappings)
        {
            _state.rebindMap.Clear();

            if (mappings != null)
            {
                foreach ((string sourceProperty, string targetProperty) in mappings)
                {
                    if (!string.IsNullOrEmpty(sourceProperty) && !string.IsNullOrEmpty(targetProperty))
                    {
                        _state.rebindMap[sourceProperty] = targetProperty;
                    }
                }
            }

            Log("Rebind map updated.", MaterialLogType.Info, 1.5f);
            NotifyChanged();
        }

        public MaterialActionDryRunSummary GetRevertDryRunSummary()
        {
            IReadOnlyList<MaterialListItem> targets = GetActionTargets(_state.revertVisibleOnly);
            return _mutationService.BuildDryRunSummary(targets, _state.filterPropertyName, _state.sourceRootMaterial);
        }

        public MaterialActionDryRunSummary GetReparentDryRunSummary()
        {
            IReadOnlyList<MaterialListItem> targets = GetActionTargets(_state.reparentVisibleOnly);
            return _mutationService.BuildDryRunSummary(targets, null, _state.sourceRootMaterial);
        }

        public void LogExternalFolderRejected()
        {
            Log("Folder must be inside this Unity project's Assets directory.", MaterialLogType.Error, 2f);
            NotifyChanged();
        }

        private IReadOnlyList<MaterialListItem> GetActionTargets(bool onlyVisible)
        {
            if (!onlyVisible)
            {
                return _state.foundMaterials;
            }

            return _state.visibleMaterials
                .Where(item => item.Included)
                .ToList();
        }

        private void FindMaterialsInternal()
        {
            MaterialQueryResult result = _queryService.QueryMaterials(
                _state.sourceShader,
                _state.searchScope,
                _state.folderPath);

            _state.foundMaterials.Clear();
            _state.foundMaterials.AddRange(result.Items);

            _state.sourceRootMaterial = result.SourceRootMaterial;
            _state.variantChildrenCount = result.VariantChildrenCount;

            RefreshVisible();
        }

        private void BuildDefaultRebindMap()
        {
            _state.rebindMap.Clear();

            if (_state.sourceShader == null || _state.targetShader == null)
            {
                return;
            }

            List<ShaderPropertyDescriptor> sourceProperties = _shaderPropertyService.GetProperties(_state.sourceShader, true);
            foreach (ShaderPropertyDescriptor sourceProperty in sourceProperties)
            {
                if (!_shaderPropertyService.TryGetPropertyType(_state.targetShader, sourceProperty.Name, out var targetType))
                {
                    continue;
                }

                if (_shaderPropertyService.AreTypesCompatible(sourceProperty.Type, targetType))
                {
                    _state.rebindMap[sourceProperty.Name] = sourceProperty.Name;
                }
            }
        }

        private void RefreshVisible()
        {
            _filterService.RebuildVisible(_state);
        }

        private void Log(string message, MaterialLogType type, float durationSeconds)
        {
            _state.lastLogMessage = message;
            _state.lastLogType = type;
            _state.lastLogDurationSeconds = Mathf.Max(0f, durationSeconds);
            _state.lastLogSequence++;
        }

        private void SetActionSummary(string message, MaterialLogType type)
        {
            _state.actionSummaryMessage = message;
            _state.actionSummaryType = type;
        }

        private void NotifyChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
