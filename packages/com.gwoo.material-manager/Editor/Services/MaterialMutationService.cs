using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GWOO.Editor.Tools
{
    internal sealed class MaterialMutationService
    {
        public MaterialActionDryRunSummary BuildDryRunSummary(
            IEnumerable<MaterialListItem> items,
            string filteredProperty,
            Material sourceRootMaterial)
        {
            List<Material> materials = ExtractMaterials(items);
            int revertableOverridesCount = CountRevertableOverrides(materials, filteredProperty);
            int reparentCandidatesCount = FindAncestors(materials, sourceRootMaterial).Count;

            return new MaterialActionDryRunSummary(materials.Count, revertableOverridesCount, reparentCandidatesCount);
        }

        public int RevertIdenticalOverrides(IEnumerable<MaterialListItem> items, string filteredProperty)
        {
            List<Material> materials = ExtractMaterials(items);
            int revertedCount = RevertIdenticalOverridesInternal(materials, filteredProperty);

            if (revertedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return revertedCount;
        }

        public MaterialMutationResult ReparentMaterials(
            Shader sourceShader,
            Material sourceRootMaterial,
            Shader targetShader,
            IReadOnlyDictionary<string, string> reboundRefs,
            IEnumerable<MaterialListItem> items)
        {
            if (sourceShader == null || targetShader == null)
            {
                return new MaterialMutationResult(0, 0);
            }

            List<Material> targetMaterials = ExtractMaterials(items);
            if (targetMaterials.Count == 0)
            {
                return new MaterialMutationResult(0, 0);
            }

            Dictionary<Material, Dictionary<string, object>> backup = BackupMappedValues(targetMaterials, reboundRefs);
            List<Material> ancestors = FindAncestors(targetMaterials, sourceRootMaterial);

            string targetShaderPath = AssetDatabase.GetAssetPath(targetShader);
            Material targetRootMaterial = string.IsNullOrEmpty(targetShaderPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Material>(targetShaderPath);

            int reparentedCount = 0;
            foreach (Material ancestor in ancestors)
            {
                Undo.RecordObject(ancestor, "Reparent Materials To Shader");

                if (targetRootMaterial != null)
                {
                    ancestor.parent = targetRootMaterial;
                }
                else
                {
                    ancestor.parent = null;
                    ancestor.shader = targetShader;
                }

                EditorUtility.SetDirty(ancestor);
                reparentedCount++;
            }

            ApplyReboundValues(targetMaterials, backup, reboundRefs);
            int revertedCount = RevertIdenticalOverridesInternal(targetMaterials, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new MaterialMutationResult(reparentedCount, revertedCount);
        }

        private static Dictionary<Material, Dictionary<string, object>> BackupMappedValues(
            IEnumerable<Material> materials,
            IReadOnlyDictionary<string, string> reboundRefs)
        {
            Dictionary<Material, Dictionary<string, object>> backup = new();

            if (reboundRefs == null || reboundRefs.Count == 0)
            {
                return backup;
            }

            foreach (Material material in materials)
            {
                Dictionary<string, object> propertyValues = new();

                foreach (string oldProperty in reboundRefs.Keys)
                {
                    int oldIndex = material.shader.FindPropertyIndex(oldProperty);
                    if (oldIndex < 0)
                    {
                        continue;
                    }

                    ShaderPropertyType type = material.shader.GetPropertyType(oldIndex);
                    propertyValues[oldProperty] = GetPropertyValue(material, oldProperty, type);
                }

                backup[material] = propertyValues;
            }

            return backup;
        }

        private static List<Material> FindAncestors(IEnumerable<Material> materials, Material sourceRootMaterial)
        {
            HashSet<Material> uniqueAncestors = new();

            foreach (Material material in materials)
            {
                Material current = material;

                while (current != null
                       && current.isVariant
                       && current.parent != sourceRootMaterial)
                {
                    current = current.parent;
                }

                if (current != null)
                {
                    uniqueAncestors.Add(current);
                }
            }

            return uniqueAncestors.ToList();
        }

        private static void ApplyReboundValues(
            IEnumerable<Material> materials,
            IReadOnlyDictionary<Material, Dictionary<string, object>> backup,
            IReadOnlyDictionary<string, string> reboundRefs)
        {
            if (reboundRefs == null || reboundRefs.Count == 0)
            {
                return;
            }

            foreach (Material material in materials)
            {
                if (!backup.TryGetValue(material, out Dictionary<string, object> oldValues))
                {
                    continue;
                }

                Undo.RecordObject(material, "Rebind Shader Properties");

                foreach ((string oldProperty, string newProperty) in reboundRefs)
                {
                    if (string.IsNullOrEmpty(newProperty) || !oldValues.TryGetValue(oldProperty, out object oldValue))
                    {
                        continue;
                    }

                    int newIndex = material.shader.FindPropertyIndex(newProperty);
                    if (newIndex < 0)
                    {
                        continue;
                    }

                    ShaderPropertyType newType = material.shader.GetPropertyType(newIndex);
                    SetPropertyValue(material, newProperty, oldValue, newType);
                }

                EditorUtility.SetDirty(material);
            }
        }

        private static int RevertIdenticalOverridesInternal(IEnumerable<Material> materials, string filteredProperty)
        {
            int revertedCount = 0;
            List<int> revertablePropertyIds = new();

            foreach (Material material in materials)
            {
                if (material == null || !material.isVariant || material.parent == null)
                {
                    continue;
                }

                revertablePropertyIds.Clear();
                CollectRevertablePropertyIds(material, material.parent, filteredProperty, revertablePropertyIds);

                if (revertablePropertyIds.Count == 0)
                {
                    continue;
                }

                Undo.RecordObject(material, "Revert Identical Material Overrides");

                foreach (int propertyId in revertablePropertyIds)
                {
                    material.RevertPropertyOverride(propertyId);
                }

                revertedCount += revertablePropertyIds.Count;

                EditorUtility.SetDirty(material);
            }

            return revertedCount;
        }

        private static int CountRevertableOverrides(IEnumerable<Material> materials, string filteredProperty)
        {
            int revertableCount = 0;
            List<int> revertablePropertyIds = new();

            foreach (Material material in materials)
            {
                if (material == null || !material.isVariant || material.parent == null)
                {
                    continue;
                }

                revertablePropertyIds.Clear();
                CollectRevertablePropertyIds(material, material.parent, filteredProperty, revertablePropertyIds);
                revertableCount += revertablePropertyIds.Count;
            }

            return revertableCount;
        }

        private static void CollectRevertablePropertyIds(
            Material material,
            Material parentMaterial,
            string filteredProperty,
            List<int> revertablePropertyIds)
        {
            if (string.IsNullOrEmpty(filteredProperty))
            {
                Shader shader = material.shader;
                int propertyCount = shader.GetPropertyCount();

                for (int i = 0; i < propertyCount; i++)
                {
                    string propertyName = shader.GetPropertyName(i);
                    if (propertyName == "_QueueControl")
                    {
                        continue;
                    }

                    int propertyId = Shader.PropertyToID(propertyName);
                    if (!material.IsPropertyOverriden(propertyId))
                    {
                        continue;
                    }

                    ShaderPropertyType propertyType = shader.GetPropertyType(i);
                    if (CanRevertIdentical(material, parentMaterial, propertyType, propertyId))
                    {
                        revertablePropertyIds.Add(propertyId);
                    }
                }

                return;
            }

            int filteredPropertyId = Shader.PropertyToID(filteredProperty);
            if (!material.IsPropertyOverriden(filteredPropertyId))
            {
                return;
            }

            int propertyIndex = material.shader.FindPropertyIndex(filteredProperty);
            if (propertyIndex < 0)
            {
                return;
            }

            ShaderPropertyType filteredType = material.shader.GetPropertyType(propertyIndex);
            if (CanRevertIdentical(material, parentMaterial, filteredType, filteredPropertyId))
            {
                revertablePropertyIds.Add(filteredPropertyId);
            }
        }

        private static bool CanRevertIdentical(
            Material material,
            Material parentMaterial,
            ShaderPropertyType propertyType,
            int propertyId)
        {
            switch (propertyType)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return Mathf.Approximately(material.GetFloat(propertyId), parentMaterial.GetFloat(propertyId));
                case ShaderPropertyType.Int:
                    return material.GetInteger(propertyId) == parentMaterial.GetInteger(propertyId);
                case ShaderPropertyType.Color:
                    return material.GetColor(propertyId) == parentMaterial.GetColor(propertyId);
                case ShaderPropertyType.Vector:
                    return material.GetVector(propertyId) == parentMaterial.GetVector(propertyId);
                case ShaderPropertyType.Texture:
                    return material.GetTexture(propertyId) == parentMaterial.GetTexture(propertyId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(propertyType), propertyType, null);
            }
        }

        private static object GetPropertyValue(Material material, string propertyName, ShaderPropertyType propertyType)
        {
            return propertyType switch
            {
                ShaderPropertyType.Float or ShaderPropertyType.Range => material.GetFloat(propertyName),
                ShaderPropertyType.Int => material.GetInteger(propertyName),
                ShaderPropertyType.Color => material.GetColor(propertyName),
                ShaderPropertyType.Vector => material.GetVector(propertyName),
                ShaderPropertyType.Texture => material.GetTexture(propertyName),
                _ => null,
            };
        }

        private static void SetPropertyValue(Material material, string propertyName, object value, ShaderPropertyType propertyType)
        {
            if (value == null)
            {
                return;
            }

            switch (propertyType)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    if (value is float floatValue && !Mathf.Approximately(material.GetFloat(propertyName), floatValue))
                    {
                        material.SetFloat(propertyName, floatValue);
                    }

                    break;
                case ShaderPropertyType.Int:
                    if (value is int intValue && material.GetInteger(propertyName) != intValue)
                    {
                        material.SetInteger(propertyName, intValue);
                    }

                    break;
                case ShaderPropertyType.Color:
                    if (value is Color colorValue && material.GetColor(propertyName) != colorValue)
                    {
                        material.SetColor(propertyName, colorValue);
                    }

                    break;
                case ShaderPropertyType.Vector:
                    if (value is Vector4 vectorValue && material.GetVector(propertyName) != vectorValue)
                    {
                        material.SetVector(propertyName, vectorValue);
                    }

                    break;
                case ShaderPropertyType.Texture:
                    if (value is Texture textureValue && material.GetTexture(propertyName) != textureValue)
                    {
                        material.SetTexture(propertyName, textureValue);
                    }

                    break;
            }
        }

        private static List<Material> ExtractMaterials(IEnumerable<MaterialListItem> items)
        {
            return items
                .Where(item => item?.Material != null)
                .Select(item => item.Material)
                .ToList();
        }
    }
}
