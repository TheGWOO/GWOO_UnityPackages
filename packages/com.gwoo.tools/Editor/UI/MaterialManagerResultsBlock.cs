using System;
using System.Collections.Generic;
using GWOO.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    public class MaterialManagerResultsBlock : VisualElement
    {
        private readonly ListView _listView;
        private readonly List<MaterialListItem> _viewItems = new();
        private readonly VisualElement _selectionRow;

        public event Action<MaterialListItem, bool> MaterialIncludeChanged;
        public event Action SelectAllClicked;
        public event Action SelectVisibleClicked;

        public MaterialManagerResultsBlock()
        {
            AddToClassList("mm-section");
            AddToClassList("mm-column");
            AddToClassList("mm-grow");
            AddToClassList("mm-results-panel");

            Label interactionsHint = new("Left click: ping + select. Right click: include/exclude for visible-only operations.");
            interactionsHint.AddToClassList("mm-microcopy");
            interactionsHint.AddToClassList("mm-results-hint");
            Add(interactionsHint);

            _listView = new ListView
            {
                fixedItemHeight = 21,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
            };
            _listView.selectionChanged += _ => _listView.RefreshItems();
            _listView.AddToClassList("mm-list");
            _listView.AddToClassList("unity-bg-color");
            Add(_listView);

            _selectionRow = new VisualElement();
            _selectionRow.AddToClassList("mm-row");
            _selectionRow.AddToClassList("mm-selection-row");
            _selectionRow.AddToClassList("mm-results-selection-row");

            CustomButton selectAllButton = new(() => SelectAllClicked?.Invoke())
            {
                text = "Select all",
                Width = 0,
                tooltip = "Select every queried material in the Project window.",
            };
            selectAllButton.AddToClassList("mm-flex");
            selectAllButton.AddToClassList("mm-select-all");
            _selectionRow.Add(selectAllButton);

            CustomButton selectVisibleButton = new(() => SelectVisibleClicked?.Invoke())
            {
                text = "Select visible",
                Width = 0,
                tooltip = "Select only currently visible and included materials.",
            };
            selectVisibleButton.AddToClassList("mm-flex");
            selectVisibleButton.AddToClassList("mm-select-visible");
            _selectionRow.Add(selectVisibleButton);

            Add(_selectionRow);
        }

        public void SetItems(IEnumerable<MaterialListItem> items)
        {
            _viewItems.Clear();
            if (items != null)
            {
                _viewItems.AddRange(items);
            }

            _listView.itemsSource = _viewItems;
            _listView.selectedIndex = -1;
            _listView.Rebuild();
        }

        public void SetSelectionActionsEnabled(bool enabled)
        {
            _selectionRow.SetEnabled(enabled);
        }

        private VisualElement MakeRow()
        {
            VisualElement row = new();
            row.AddToClassList("mm-row");
            row.AddToClassList("mm-result-row");
            row.focusable = false;

            ObjectField materialField = new()
            {
                objectType = typeof(Material),
                allowSceneObjects = false,
                focusable = true,
                tooltip = "Left click to ping. Right click to include/exclude from visible-only actions.",
                style =
                {
                    flexGrow = 1f,
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                },
            };
            materialField.AddToClassList("mm-material-field");
            materialField.SetEnabled(false);
            row.Add(materialField);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (row.userData is not MaterialListItem item || item.Material == null)
                {
                    return;
                }

                if (evt.button == 0)
                {
                    _listView.selectedIndex = _viewItems.IndexOf(item);
                    materialField.Focus();
                    EditorGUIUtility.PingObject(item.Material);
                }
                else if (evt.button == 1)
                {
                    item.Included = !item.Included;
                    ApplyRowIncludedState(row, materialField, item.Included);
                    MaterialIncludeChanged?.Invoke(item, item.Included);
                    evt.StopPropagation();
                }
            });

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (_listView.itemsSource == null || index < 0 || index >= _listView.itemsSource.Count)
            {
                return;
            }

            if (_listView.itemsSource[index] is not MaterialListItem item)
            {
                return;
            }

            ObjectField materialField = element.ElementAt(0) as ObjectField;

            element.userData = item;

            if (materialField != null)
            {
                materialField.SetValueWithoutNotify(item.Material);
                ApplyRowIncludedState(element, materialField, item.Included);
            }

            if (index == _listView.selectedIndex)
            {
                element.AddToClassList("mm-selected-item");
            }
            else
            {
                element.RemoveFromClassList("mm-selected-item");
            }
        }

        private static void ApplyRowIncludedState(VisualElement row, ObjectField field, bool included)
        {
            field.SetEnabled(included);

            if (included)
            {
                row.RemoveFromClassList("mm-item-disabled");
            }
            else
            {
                row.AddToClassList("mm-item-disabled");
            }
        }
    }
}
