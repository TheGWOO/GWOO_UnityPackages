using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public class ColorPickerPopup : PopupWindow
    {
        private Color _currentColor;
        private Color _previousColor;

        private ColorWheel _colorWheel;
        private VisualElement _colorPreview;

        private Slider _redSlider;
        private Slider _greenSlider;
        private Slider _blueSlider;
        private Slider _alphaSlider;

        public ColorPickerPopup(Color initialColor, Action<Color> onColorSelected)
        {
            _currentColor = initialColor;
            _previousColor = initialColor;

            ContentContainer.style.width = 250;
            ContentContainer.style.flexDirection = FlexDirection.Column;

            AddColorWheel();

            _redSlider = CreateColorSlider("R", _currentColor.r);
            _greenSlider = CreateColorSlider("G", _currentColor.g);
            _blueSlider = CreateColorSlider("B", _currentColor.b);
            _alphaSlider = CreateColorSlider("A", _currentColor.a);

            ContentContainer.Add(_redSlider);
            ContentContainer.Add(_greenSlider);
            ContentContainer.Add(_blueSlider);
            ContentContainer.Add(_alphaSlider);

            AddColorPreview();

            VisualElement buttonsContainer = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 10,
                }
            };
            ContentContainer.Add(buttonsContainer);

            CustomButton okButton = new("primary-color")
            { 
                text = "OK",
                style =
                {
                    flexGrow = 1,
                    marginRight = 5,
                    width = new StyleLength(Length.Percent(45))
                }
            };
            okButton.RegisterCallback<ClickEvent>(evt => { onColorSelected?.Invoke(_currentColor); Close(); });
            buttonsContainer.Add(okButton);

            CustomButton cancelButton = new("secondary-color")
            { 
                text = "Annuler",
                style =
                {
                    flexGrow = 1,
                    width = new StyleLength(Length.Percent(45))
                }
            };
            cancelButton.RegisterCallback<ClickEvent>(evt => { Close(); });
            buttonsContainer.Add(cancelButton);

            _redSlider.RegisterValueChangedCallback(evt =>
            {
                _currentColor.r = evt.newValue;
                UpdateColorPreview();
                _colorWheel.SetColor(_currentColor);
            });

            _greenSlider.RegisterValueChangedCallback(evt =>
            {
                _currentColor.g = evt.newValue;
                UpdateColorPreview();
                _colorWheel.SetColor(_currentColor);
            });

            _blueSlider.RegisterValueChangedCallback(evt =>
            {
                _currentColor.b = evt.newValue;
                UpdateColorPreview();
                _colorWheel.SetColor(_currentColor);
            });

            _alphaSlider.RegisterValueChangedCallback(evt =>
            {
                _currentColor.a = evt.newValue;
                UpdateColorPreview();
            });
        }

        private void AddColorWheel()
        {
            _colorWheel = new()
            {
                style =
                {
                    alignSelf = Align.Center,
                    marginBottom = 10
                }
            };

            _colorWheel.SetColor(_currentColor);

            _colorWheel.OnColorSelected += color =>
            {
                _currentColor.r = color.r;
                _currentColor.g = color.g;
                _currentColor.b = color.b;
                UpdateColorPreview();
                UpdateSliders();
            };

            ContentContainer.Add(_colorWheel);
        }

        private void AddColorPreview()
        {
            // Créer un conteneur pour la prévisualisation
            _colorPreview = new()
            {
                style =
                {
                    height = 50,
                    marginTop = 10,
                    flexDirection = FlexDirection.Row
                }
            };

            // Ancienne couleur
            VisualElement oldColorElement = new()
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = _previousColor,
                    borderTopLeftRadius = 5,
                    borderBottomLeftRadius = 5
                }
            };

            // Nouvelle couleur
            VisualElement newColorElement = new()
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = _currentColor,
                    borderTopRightRadius = 5,
                    borderBottomRightRadius = 5
                }
            };

            _colorPreview.Add(oldColorElement);
            _colorPreview.Add(newColorElement);

            ContentContainer.Add(_colorPreview);
        }

        private void UpdateColorPreview()
        {
            if (_colorPreview != null)
            {
                VisualElement newColorElement = _colorPreview.ElementAt(1);
                newColorElement.style.backgroundColor = _currentColor;
            }
        }

        private void UpdateSliders()
        {
            _redSlider.SetValueWithoutNotify(_currentColor.r);
            _greenSlider.SetValueWithoutNotify(_currentColor.g);
            _blueSlider.SetValueWithoutNotify(_currentColor.b);
            _alphaSlider.SetValueWithoutNotify(_currentColor.a);
        }

        private Slider CreateColorSlider(string label, float initialValue)
        {
            Slider slider = new(label, 0, 1)
            {
                showInputField = true,
                value = initialValue,
                style = { flexGrow = 1, marginBottom = 5 }
            };
            slider.labelElement.style.minWidth = 20;
            slider.labelElement.style.marginRight = 5;
            return slider;
        }
    }
}