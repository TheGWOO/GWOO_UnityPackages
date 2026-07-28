using UnityEngine.Rendering;

namespace GWOO.Editor.Tools
{
    internal readonly struct ShaderPropertyDescriptor
    {
        public ShaderPropertyDescriptor(string name, string displayName, ShaderPropertyType type)
        {
            Name = name;
            DisplayName = displayName;
            Type = type;
        }

        public string Name { get; }
        public string DisplayName { get; }
        public ShaderPropertyType Type { get; }
    }
}
