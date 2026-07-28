using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public sealed class CustomColorField : VisualElement
    {
        private VisualElement _colorDisplay;
        private VisualElement _alphaProgressBar;
        private Label _fieldLabel;
        private Color _currentColor = Color.white;
        public Color CurrentColor
        {
            get => _currentColor;
            set
            {
                if (_currentColor != value)
                {
                    Color oldColor = _currentColor;
                    _currentColor = value;
                    _colorDisplay.style.backgroundColor = new StyleColor(new Color(value.r, value.g, value.b, 1f));
                    _alphaProgressBar.style.width = new StyleLength(Length.Percent(value.a * 100f));

                    using ChangeEvent<Color> evt = ChangeEvent<Color>.GetPooled(oldColor, value);
                    evt.target = this;
                    SendEvent(evt);
                }
            }
        }
        
        public CustomColorField(string label = null)
        {
            AddToClassList("unity-base-field");
            AddToClassList("unity-base-text-field");
            focusable = true;
            Label = label;

            Initialize();
        }

        public string Label
        {
            get => _fieldLabel?.text;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _fieldLabel?.RemoveFromHierarchy();
                    _fieldLabel = null;
                }
                else
                {
                    if (_fieldLabel == null)
                    {
                        _fieldLabel = new Label(value);
                        _fieldLabel.AddToClassList("unity-base-field__label");
                        Insert(0, _fieldLabel);
                    }
                    else
                    {
                        _fieldLabel.text = value;
                    }
                }
            }
        }

        private void Initialize()
        {
            style.alignItems = new StyleEnum<Align>(Align.Center);
            
            VisualElement colorContainer = new();
            colorContainer.AddToClassList("custom-color-field__container");
            colorContainer.RegisterCallback<ClickEvent>(evt => { OpenColorPicker(); });
            Add(colorContainer);
            
            _colorDisplay = new()
                { 
                    style = 
                    { 
                        backgroundColor = new StyleColor(new Color(_currentColor.r, _currentColor.g, _currentColor.b, 1f))
                    } 
                };
            _colorDisplay.AddToClassList("custom-color-field__color-preview");
            colorContainer.Add(_colorDisplay);

            VisualElement alphaProgressBarContainer = new();
            alphaProgressBarContainer.AddToClassList("custom-color-field__alpha-container");
            colorContainer.Add(alphaProgressBarContainer);

            _alphaProgressBar = new()
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new StyleColor(Color.white),
                    width = new StyleLength(Length.Percent(_currentColor.a * 100f))
                }
            };
            alphaProgressBarContainer.Add(_alphaProgressBar);
        }

        private void OpenColorPicker()
        {
            VisualElement root = GetRootVisualElement();
            if (root != null)
            {
                ColorPickerPopup colorPicker = new(CurrentColor, color =>
                {
                    CurrentColor = color;
                });

                root.Add(colorPicker);
            }
        }

        private VisualElement GetRootVisualElement()
        {
            VisualElement ve = this;
            while (ve.parent != null)
            {
                ve = ve.parent;
            }
            return ve;
        }
    }
}
