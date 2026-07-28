using System;
using GWOO.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    public class MaterialManagerActionsBlock : VisualElement
    {
        private readonly CustomButton _revertButton;
        private readonly CustomToggle _revertVisibleOnlyToggle;
        private readonly ObjectField _targetShaderField;
        private readonly VisualElement _reparentControls;
        private readonly CustomToggle _reparentVisibleOnlyToggle;
        private readonly CustomButton _rebindButton;
        private readonly CustomButton _reparentButton;

        public event Action RevertClicked;
        public event Action<bool> RevertVisibleOnlyChanged;
        public event Action<Shader> TargetShaderChanged;
        public event Action RebindClicked;
        public event Action ReparentClicked;
        public event Action<bool> ReparentVisibleOnlyChanged;

        public MaterialManagerActionsBlock()
        {
            AddToClassList("mm-section");
            AddToClassList("mm-actions-panel");

            VisualElement revertRow = new();
            revertRow.AddToClassList("mm-row");
            revertRow.AddToClassList("mm-revert-row");
            revertRow.AddToClassList("mm-center-row");

            VisualElement revertContent = new();
            revertContent.AddToClassList("mm-row");
            revertContent.AddToClassList("mm-inline-centered-content");

            _revertButton = new(() => RevertClicked?.Invoke())
            {
                text = "Cleanup identical overrides",
                Width = 0,
                tooltip = "Revert overrides that match parent values. A dry-run summary is shown before applying.",
            };
            _revertButton.AddToClassList("mm-revert-button");
            _revertButton.AddToClassList("mm-margin-right-6");
            revertContent.Add(_revertButton);

            VisualElement revertToggleGroup = new();
            revertToggleGroup.AddToClassList("mm-row");
            revertToggleGroup.AddToClassList("mm-inline-group");

            _revertVisibleOnlyToggle = new CustomToggle()
            {
                tooltip = "Apply revert only to currently visible and included materials.",
            };
            _revertVisibleOnlyToggle.AddToClassList("mm-margin-right-4");
            _revertVisibleOnlyToggle.RegisterValueChangedCallback(evt => RevertVisibleOnlyChanged?.Invoke(evt.newValue));
            revertToggleGroup.Add(_revertVisibleOnlyToggle);

            Label revertVisibleLabel = new("Visible only")
            {
                tooltip = "Limit cleanup to currently visible and included materials.",
            };
            revertToggleGroup.Add(revertVisibleLabel);
            revertContent.Add(revertToggleGroup);
            revertRow.Add(revertContent);

            Add(revertRow);

            Add(MaterialManagerUIFactory.CreateSeparator());

            _targetShaderField = new ObjectField("New shader")
            {
                objectType = typeof(Shader),
                allowSceneObjects = false,
                tooltip = "Target shader used for material reparenting.",
            };
            _targetShaderField.AddToClassList("mm-target-shader-field");
            _targetShaderField.RegisterValueChangedCallback(evt => TargetShaderChanged?.Invoke(evt.newValue as Shader));
            Add(_targetShaderField);

            _reparentControls = new VisualElement();
            _reparentControls.style.display = DisplayStyle.None;
            _reparentControls.AddToClassList("mm-column");
            Add(_reparentControls);

            _reparentButton = new CustomButton(() => ReparentClicked?.Invoke())
            {
                text = "Reparent materials to new shader",
                Width = 75,
                tooltip = "Reparent candidates to the new shader root material (or direct shader if no root asset).",
            };
            _reparentButton.AddToClassList("mm-cta");
            _reparentButton.AddToClassList("big");
            _reparentButton.AddToClassList("align-center");
            _reparentButton.AddToClassList("mm-reparent-button");
            _reparentControls.Add(_reparentButton);

            VisualElement reparentOptionsRow = new();
            reparentOptionsRow.AddToClassList("mm-row");
            reparentOptionsRow.AddToClassList("mm-center-row");

            VisualElement reparentOptionsContent = new();
            reparentOptionsContent.AddToClassList("mm-row");
            reparentOptionsContent.AddToClassList("mm-inline-centered-content");

            _rebindButton = new CustomButton(() => RebindClicked?.Invoke())
            {
                text = "Rebind properties",
                Width = 0,
                tooltip = "Open property mapping to copy compatible values during reparent.",
            };
            _rebindButton.AddToClassList("mm-rebind-button");
            _rebindButton.AddToClassList("mm-margin-right-6");
            reparentOptionsContent.Add(_rebindButton);

            VisualElement reparentToggleGroup = new();
            reparentToggleGroup.AddToClassList("mm-row");
            reparentToggleGroup.AddToClassList("mm-inline-group");

            _reparentVisibleOnlyToggle = new CustomToggle()
            {
                tooltip = "Reparent only currently visible and included materials.",
            };
            _reparentVisibleOnlyToggle.AddToClassList("mm-margin-right-4");
            _reparentVisibleOnlyToggle.RegisterValueChangedCallback(evt => ReparentVisibleOnlyChanged?.Invoke(evt.newValue));
            reparentToggleGroup.Add(_reparentVisibleOnlyToggle);

            Label reparentVisibleLabel = new("Visible only")
            {
                tooltip = "Limit reparent to currently visible and included materials.",
            };
            reparentToggleGroup.Add(reparentVisibleLabel);

            reparentOptionsContent.Add(reparentToggleGroup);
            reparentOptionsRow.Add(reparentOptionsContent);
            _reparentControls.Add(reparentOptionsRow);

            Label reparentHint = new("Visible-only applies to currently filtered and included materials.");
            reparentHint.AddToClassList("mm-microcopy");
            _reparentControls.Add(reparentHint);

            Label cleanupHint = new("Cleanup reverts identical variant overrides and never changes unique values.");
            cleanupHint.AddToClassList("mm-microcopy");
            Add(cleanupHint);
        }

        public void SetTargetShader(Shader shader)
        {
            _targetShaderField.SetValueWithoutNotify(shader);
        }

        public void SetRevertVisibleOnly(bool value)
        {
            _revertVisibleOnlyToggle.SetValueWithoutNotify(value);
        }

        public void SetRevertEnabled(bool enabled)
        {
            _revertButton.SetEnabled(enabled);
            _revertVisibleOnlyToggle.SetEnabled(enabled);
        }

        public void SetRevertButtonText(string filteredPropertyName)
        {
            _revertButton.text = string.IsNullOrEmpty(filteredPropertyName)
                ? "Cleanup identical overrides"
                : $"Cleanup identical {TrimLabel(filteredPropertyName, 18)}";
        }

        private static string TrimLabel(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            {
                return value;
            }

            return value.Substring(0, maxChars) + "...";
        }

        public void SetReparentVisibleOnly(bool value)
        {
            _reparentVisibleOnlyToggle.SetValueWithoutNotify(value);
        }

        public void SetReparentControlsDisplay(bool displayed)
        {
            _reparentControls.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetRebindEnabled(bool enabled)
        {
            _rebindButton.SetEnabled(enabled);
        }

        public void SetReparentEnabled(bool enabled)
        {
            _reparentButton.SetEnabled(enabled);
        }
    }
}
