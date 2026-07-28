using System;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    internal sealed class SearchFieldController : IDisposable
    {
        private readonly TextField _field;
        private readonly Action<string> _onQueryChanged;
        private readonly Action<bool> _onHasQueryChanged;

        private bool _ignoreFieldChange;

        public SearchFieldController(
            TextField field,
            string placeholder,
            string tooltip,
            Action<string> onQueryChanged,
            Action<bool> onHasQueryChanged = null)
        {
            _field = field;
            _onQueryChanged = onQueryChanged;
            _onHasQueryChanged = onHasQueryChanged;

            _field.tooltip = tooltip;
            _field.textEdition.placeholder = placeholder ?? string.Empty;
            _field.textEdition.hidePlaceholderOnFocus = true;
            _field.RegisterValueChangedCallback(OnFieldChanged);

            SetQuery(string.Empty);
        }

        public void Dispose()
        {
            _field.UnregisterValueChangedCallback(OnFieldChanged);
        }

        public void SetQuery(string query)
        {
            string normalized = query ?? string.Empty;

            _ignoreFieldChange = true;
            _field.SetValueWithoutNotify(normalized);
            _ignoreFieldChange = false;
            _onHasQueryChanged?.Invoke(!string.IsNullOrEmpty(normalized));
        }

        public void ClearAndNotify()
        {
            SetQuery(string.Empty);
            _onQueryChanged?.Invoke(string.Empty);
        }

        private void OnFieldChanged(ChangeEvent<string> evt)
        {
            if (_ignoreFieldChange)
            {
                return;
            }

            string query = evt.newValue ?? string.Empty;
            _onQueryChanged?.Invoke(query);
            _onHasQueryChanged?.Invoke(!string.IsNullOrEmpty(query));
        }
    }
}
