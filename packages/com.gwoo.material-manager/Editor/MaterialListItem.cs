using UnityEngine;

namespace GWOO.Editor.Tools
{
    public sealed class MaterialListItem
    {
        public MaterialListItem(Material material)
        {
            Material = material;
            Included = true;
        }

        public Material Material { get; }
        public bool Included { get; set; }
    }
}
