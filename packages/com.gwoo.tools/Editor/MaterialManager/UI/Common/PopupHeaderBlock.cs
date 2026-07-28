using System;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    [UxmlElement]
    public partial class PopupHeaderBlock : VisualElement
    {
        private readonly Label _titleLabel;
        private readonly Button _optionsButton;

        public event Action OptionsClicked;

        public PopupHeaderBlock()
        {
            AddToClassList("mm-row");

            _titleLabel = new Label();
            _titleLabel.AddToClassList("mm-subtitle");
            _titleLabel.AddToClassList("mm-popup-title");
            Add(_titleLabel);

            _optionsButton = new Button(() => OptionsClicked?.Invoke())
            {
                text = "⋮",
            };
            _optionsButton.AddToClassList("mm-options-button");
            Add(_optionsButton);
        }

        public void SetTitle(string title)
        {
            _titleLabel.text = title;
        }

        public void SetOptionsTooltip(string buttonTooltip)
        {
            _optionsButton.tooltip = buttonTooltip;
        }
    }
}
