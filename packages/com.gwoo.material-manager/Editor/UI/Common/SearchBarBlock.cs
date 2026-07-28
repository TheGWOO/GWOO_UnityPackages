using System;
using GWOO.UIElements;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    [UxmlElement]
    public partial class SearchBarBlock : VisualElement
    {
        private readonly TextField _searchField;
        private readonly CustomButton _clearButton;

        private SearchFieldController _searchController;

        public event Action<string> QueryChanged;

        public SearchBarBlock()
        {
            AddToClassList("mm-row");
            AddToClassList("mm-search-bar");

            _searchField = new TextField
            {
                value = string.Empty,
            };
            _searchField.AddToClassList("mm-no-label-field");
            _searchField.AddToClassList("mm-search-field");
            _searchField.AddToClassList("mm-flex");
            Add(_searchField);

            _clearButton = new CustomButton(ClearAndNotify)
            {
                text = "Clear",
                Width = 0,
            };
            _clearButton.SetEnabled(false);
            Add(_clearButton);
        }

        public void Configure(string placeholder, string searchbarTooltip)
        {
            _searchController?.Dispose();
            _searchController = new SearchFieldController(
                _searchField,
                placeholder,
                searchbarTooltip,
                query => QueryChanged?.Invoke(query),
                hasQuery => _clearButton.SetEnabled(hasQuery));
        }

        public void SetQuery(string query)
        {
            EnsureConfigured();
            _searchController.SetQuery(query);
        }

        public void ClearAndNotify()
        {
            EnsureConfigured();
            _searchController.ClearAndNotify();
        }

        private void EnsureConfigured()
        {
            if (_searchController == null)
            {
                Configure("Search...", string.Empty);
            }
        }
    }
}
