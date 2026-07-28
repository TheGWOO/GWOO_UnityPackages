using System;
using System.Collections.Generic;
using GWOO.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    public class MaterialManagerQueryBlock : VisualElement
    {
        private readonly Label _folderPathLabel;
        private readonly DropdownField _scopeDropdown;
        private readonly VisualElement _folderRow;
        private readonly ObjectField _sourceShaderField;
        private readonly CustomButton _findButton;

        public event Action<MaterialSearchScope> ScopeChanged;
        public event Action BrowseFolderClicked;
        public event Action DefaultFolderClicked;
        public event Action<Shader> SourceShaderChanged;
        public event Action FindClicked;

        public MaterialManagerQueryBlock()
        {
            AddToClassList("mm-section");
            AddToClassList("mm-top-panel");

            VisualElement scopeRow = new();
            scopeRow.AddToClassList("mm-row");
            scopeRow.AddToClassList("mm-align-end");

            Label searchInLabel = new("Search in:");
            searchInLabel.AddToClassList("mm-margin-right-4");
            scopeRow.Add(searchInLabel);

            _scopeDropdown = new DropdownField(new List<string> { "Folder", "Scene" }, 0)
            {
                focusable = false,
                tooltip = "Folder: query project assets. Scene: query materials used by active scene renderers.",
            };
            _scopeDropdown.AddToClassList("mm-scope-dropdown");
            _scopeDropdown.RegisterValueChangedCallback(_ => ScopeChanged?.Invoke((MaterialSearchScope)_scopeDropdown.index));
            scopeRow.Add(_scopeDropdown);
            Add(scopeRow);

            _folderRow = new VisualElement();
            _folderRow.AddToClassList("mm-row");
            _folderRow.AddToClassList("mm-folder-row");

            VisualElement folderPathRow = new();
            folderPathRow.AddToClassList("mm-row");
            folderPathRow.AddToClassList("mm-flex");
            folderPathRow.AddToClassList("mm-folder-path-row");

            folderPathRow.Add(new Label("Search folder:"));

            _folderPathLabel = new Label(MaterialManagerState.DEFAULT_FOLDER_PATH);
            _folderPathLabel.AddToClassList("mm-folder-path");
            _folderPathLabel.tooltip = "Only folders inside Assets are valid search roots.";
            folderPathRow.Add(_folderPathLabel);

            _folderRow.Add(folderPathRow);

            VisualElement folderButtons = new();
            folderButtons.AddToClassList("mm-row");

            CustomButton browseButton = new(() => BrowseFolderClicked?.Invoke())
            {
                text = "Browse",
                Width = 0,
            };
            browseButton.AddToClassList("mm-margin-right-2");
            browseButton.AddToClassList("mm-no-shrink");
            browseButton.tooltip = "Choose a folder under Assets as search root.";
            folderButtons.Add(browseButton);

            CustomButton defaultButton = new(() => DefaultFolderClicked?.Invoke())
            {
                text = "Default",
                Width = 0,
            };
            defaultButton.AddToClassList("mm-no-shrink");
            defaultButton.tooltip = "Reset folder scope to the entire Assets directory.";
            folderButtons.Add(defaultButton);

            _folderRow.Add(folderButtons);

            Add(_folderRow);

            Label variantsHint = new("Variant children are counted relative to the source root material asset.");
            variantsHint.AddToClassList("mm-microcopy");
            Add(variantsHint);

            _sourceShaderField = new ObjectField("Shader")
            {
                objectType = typeof(Shader),
                allowSceneObjects = false,
                tooltip = "Source shader used to find matching materials.",
            };
            _sourceShaderField.AddToClassList("mm-source-shader-field");
            _sourceShaderField.RegisterValueChangedCallback(evt => SourceShaderChanged?.Invoke(evt.newValue as Shader));
            Add(_sourceShaderField);

            _findButton = new CustomButton(() => FindClicked?.Invoke())
            {
                text = "Find materials",
                tooltip = "Run a fresh query with current scope, folder and source shader.",
            };
            _findButton.AddToClassList("mm-cta");
            _findButton.AddToClassList("big");
            _findButton.AddToClassList("align-center");
            _findButton.AddToClassList("mm-find-button");
            Add(_findButton);
        }

        public void SetScope(MaterialSearchScope scope)
        {
            _scopeDropdown.SetValueWithoutNotify(_scopeDropdown.choices[(int)scope]);
            SetFolderControlsDisplay(scope == MaterialSearchScope.Folder);
        }

        public void SetFolderPath(string folderPath)
        {
            _folderPathLabel.text = folderPath ?? MaterialManagerState.DEFAULT_FOLDER_PATH;
        }

        public void SetSourceShader(Shader shader)
        {
            _sourceShaderField.SetValueWithoutNotify(shader);
        }

        public void SetFindEnabled(bool enabled)
        {
            _findButton.SetEnabled(enabled);
        }

        public void SetFolderControlsDisplay(bool displayed)
        {
            _folderRow.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
