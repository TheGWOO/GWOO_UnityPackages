using System.Collections.Generic;
using UnityEngine;

namespace GWOO.Editor.Tools
{
    internal readonly struct MaterialQueryResult
    {
        public MaterialQueryResult(List<MaterialListItem> items, Material sourceRootMaterial, int variantChildrenCount)
        {
            Items = items;
            SourceRootMaterial = sourceRootMaterial;
            VariantChildrenCount = variantChildrenCount;
        }

        public List<MaterialListItem> Items { get; }
        public Material SourceRootMaterial { get; }
        public int VariantChildrenCount { get; }
    }
}
