using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GWOO.Editor.Tools
{
    internal sealed class ShaderPropertyService
    {
        public List<ShaderPropertyDescriptor> GetProperties(Shader shader, bool showAllProperties)
        {
            List<ShaderPropertyDescriptor> properties = new();

            if (shader == null)
            {
                return properties;
            }

            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                string propertyName = shader.GetPropertyName(i);
                string displayName = shader.GetPropertyDescription(i);

                if (!showAllProperties && ShouldHideProperty(propertyName))
                {
                    continue;
                }

                ShaderPropertyType type = shader.GetPropertyType(i);
                properties.Add(new ShaderPropertyDescriptor(propertyName, displayName, type));
            }

            return properties;
        }

        public List<string> GetCompatibleTargetProperties(Shader targetShader, ShaderPropertyType sourceType, bool showAllProperties)
        {
            List<string> compatible = new();

            if (targetShader == null)
            {
                return compatible;
            }

            int propertyCount = targetShader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                ShaderPropertyType targetType = targetShader.GetPropertyType(i);
                if (!AreTypesCompatible(sourceType, targetType))
                {
                    continue;
                }

                string name = targetShader.GetPropertyName(i);
                if (!showAllProperties && ShouldHideProperty(name))
                {
                    continue;
                }

                compatible.Add(name);
            }

            return compatible;
        }

        public bool TryGetPropertyType(Shader shader, string propertyName, out ShaderPropertyType propertyType)
        {
            propertyType = default;

            if (shader == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            int index = shader.FindPropertyIndex(propertyName);
            if (index < 0)
            {
                return false;
            }

            propertyType = shader.GetPropertyType(index);
            return true;
        }

        public bool AreTypesCompatible(ShaderPropertyType sourceType, ShaderPropertyType targetType)
        {
            return sourceType == targetType
                   || (sourceType == ShaderPropertyType.Float && targetType == ShaderPropertyType.Range)
                   || (sourceType == ShaderPropertyType.Range && targetType == ShaderPropertyType.Float);
        }

        private static bool ShouldHideProperty(string propertyName)
        {
            return StartsWithToken(propertyName, "unity_");
        }

        private static bool StartsWithToken(string value, string token)
        {
            return !string.IsNullOrEmpty(value)
                   && value.StartsWith(token, StringComparison.OrdinalIgnoreCase);
        }
    }
}
