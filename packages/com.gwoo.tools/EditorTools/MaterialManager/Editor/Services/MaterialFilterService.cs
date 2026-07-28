using System;

namespace GWOO.Editor.Tools
{
    internal sealed class MaterialFilterService
    {
        public void RebuildVisible(MaterialManagerState state)
        {
            state.visibleMaterials.Clear();

            foreach (MaterialListItem item in state.foundMaterials)
            {
                if (MatchesFilters(state, item))
                {
                    state.visibleMaterials.Add(item);
                }
            }
        }

        private static bool MatchesFilters(MaterialManagerState state, MaterialListItem item)
        {
            if (item?.Material == null)
            {
                return false;
            }

            if (!state.showVariants
                && item.Material.isVariant
                && item.Material.parent != state.sourceRootMaterial)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(state.filterPropertyName)
                && !item.Material.IsPropertyOverriden(state.filterPropertyName))
            {
                return false;
            }

            return MatchesSearch(item.Material.name, state.searchQuery);
        }

        private static bool MatchesSearch(string materialName, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string normalizedName = materialName.ToLowerInvariant();
            string normalizedQuery = query.Trim().ToLowerInvariant();

            string[] tags = normalizedName.Split('_', '-', ' ');
            string[] terms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            bool isNameMatch = normalizedName.Contains(normalizedQuery);
            bool hasRequiredTags = true;
            bool hasExcludedTags = false;

            foreach (string term in terms)
            {
                if (term.StartsWith("-", StringComparison.Ordinal))
                {
                    string excludedTag = term.Length > 1 ? term[1..] : string.Empty;
                    if (string.IsNullOrEmpty(excludedTag))
                    {
                        continue;
                    }

                    if (Array.Exists(tags, tag => tag.Contains(excludedTag, StringComparison.Ordinal)))
                    {
                        hasExcludedTags = true;
                        break;
                    }

                    continue;
                }

                if (!Array.Exists(tags, tag => tag.Contains(term, StringComparison.Ordinal)))
                {
                    hasRequiredTags = false;
                    break;
                }
            }

            return !hasExcludedTags && (hasRequiredTags || isNameMatch);
        }
    }
}
