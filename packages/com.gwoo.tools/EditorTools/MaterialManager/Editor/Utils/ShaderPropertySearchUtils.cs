namespace GWOO.Editor.Tools
{
    internal static class ShaderPropertySearchUtils
    {
        public static bool MatchesSearch(ShaderPropertyDescriptor property, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string normalizedQuery = query.Trim().ToLowerInvariant();
            string propertyName = property.Name?.ToLowerInvariant() ?? string.Empty;
            string displayName = property.DisplayName?.ToLowerInvariant() ?? string.Empty;

            return propertyName.Contains(normalizedQuery)
                   || displayName.Contains(normalizedQuery);
        }
    }
}
