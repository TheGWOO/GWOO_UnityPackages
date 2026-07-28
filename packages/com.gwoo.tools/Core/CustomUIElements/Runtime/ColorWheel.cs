using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public class ColorWheel : VisualElement
    {
        private Texture2D _hueRingTexture;
        private Texture2D _svSquareTexture;

        private int _textureSize = 200;
        private int _squareSize = 100;
        private int _ringThickness = 20;

        public event Action<Color> OnColorSelected;

        private VisualElement _hueRingElement;
        private VisualElement _svSquareElement;

        private VisualElement _hueCursor;
        private VisualElement _svCursor;

        private bool _isHueDragging;
        private bool _isSVDragging;

        private float _hue;
        private float _saturation;
        private float _value;

        public ColorWheel()
        {
            // Initialize default HSV values
            _hue = 0f;
            _saturation = 1f;
            _value = 1f;

            // Set the size of the parent element
            style.width = _textureSize;
            style.height = _textureSize;

            // Set up the parent element to stack children and center them
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;
            style.position = Position.Relative;

            // Create the hue ring element
            _hueRingElement = new VisualElement();
            _hueRingTexture = GenerateHueRingTexture(_textureSize, _textureSize, _ringThickness);
            _hueRingElement.style.backgroundImage = new StyleBackground(_hueRingTexture);
            _hueRingElement.style.width = _textureSize;
            _hueRingElement.style.height = _textureSize;

            // Create the SV square element
            _svSquareElement = new VisualElement();
            _svSquareTexture = GenerateSVSquareTexture(_squareSize, _squareSize, _hue);
            _svSquareElement.style.backgroundImage = new StyleBackground(_svSquareTexture);
            _svSquareElement.style.width = _squareSize;
            _svSquareElement.style.height = _squareSize;

            // Create a container to center the SV square
            VisualElement svContainer = new()
            {
                style =
                {
                    width = _textureSize,
                    height = _textureSize,
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    position = Position.Absolute
                },
                pickingMode = PickingMode.Ignore
            };

            // Add the SV square to its container
            svContainer.Add(_svSquareElement);

            // Add elements to the parent VisualElement
            Add(_hueRingElement);
            Add(svContainer);

            // Create cursors
            _hueCursor = new VisualElement();
            _hueCursor.AddToClassList("custom-cursor__rounded-selector");
            _hueCursor.style.position = Position.Absolute;
            Add(_hueCursor);

            _svCursor = new VisualElement();
            _svCursor.AddToClassList("custom-cursor__rounded-selector");
            _svCursor.style.position = Position.Absolute;
            Add(_svCursor);

            // Register events for hue ring
            _hueRingElement.RegisterCallback<PointerDownEvent>(OnHueRingPointerDown);
            _hueRingElement.RegisterCallback<PointerMoveEvent>(OnHueRingPointerMove);
            _hueRingElement.RegisterCallback<PointerUpEvent>(OnHueRingPointerUp);

            // Register events for SV square
            _svSquareElement.RegisterCallback<PointerDownEvent>(OnSVSquarePointerDown);
            _svSquareElement.RegisterCallback<PointerMoveEvent>(OnSVSquarePointerMove);
            _svSquareElement.RegisterCallback<PointerUpEvent>(OnSVSquarePointerUp);

            // Set initial cursor positions
            SetColor(Color.HSVToRGB(_hue, _saturation, _value));
        }

        private Texture2D GenerateHueRingTexture(int width, int height, int ringThickness)
        {
            Texture2D tex = new(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[width * height];

            Vector2 center = new(width / 2f, height / 2f);
            float outerRadius = width / 2f;
            float innerRadius = outerRadius - ringThickness;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    Vector2 dir = pos - center;
                    float dist = dir.magnitude;

                    if (dist <= outerRadius && dist >= innerRadius)
                    {
                        float angle = Mathf.Atan2(-dir.y, dir.x);
                        if (angle < 0f)
                            angle += 2f * Mathf.PI;

                        float hue = angle / (2f * Mathf.PI);
                        Color color = Color.HSVToRGB(hue, 1f, 1f);
                        pixels[y * width + x] = color;
                    }
                    else
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return tex;
        }

        private Texture2D GenerateSVSquareTexture(int width, int height, float hue)
        {
            Texture2D tex = new(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float value = ((float)y) / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    float saturation = (float)x / (width - 1);
                    Color color = Color.HSVToRGB(hue, saturation, value);
                    pixels[y * width + x] = color;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return tex;
        }

        private void OnHueRingPointerDown(PointerDownEvent evt)
        {
            _isHueDragging = true;
            UpdateHue(evt.localPosition);
            _hueRingElement.CapturePointer(evt.pointerId);
        }

        private void OnHueRingPointerMove(PointerMoveEvent evt)
        {
            if (_isHueDragging)
            {
                UpdateHue(evt.localPosition);
            }
        }

        private void OnHueRingPointerUp(PointerUpEvent evt)
        {
            _isHueDragging = false;
            _hueRingElement.ReleasePointer(evt.pointerId);
        }

        private void UpdateHue(Vector2 localPosition)
        {
            Vector2 center = new(_textureSize / 2f, _textureSize / 2f);
            Vector2 dir = localPosition - center;
            float dist = dir.magnitude;
            float outerRadius = _textureSize / 2f;
            float innerRadius = outerRadius - _ringThickness;

            if (dist >= innerRadius && dist <= outerRadius)
            {
                float angle = Mathf.Atan2(dir.y, dir.x);
                if (angle < 0f)
                    angle += 2f * Mathf.PI;

                _hue = angle / (2f * Mathf.PI);

                _svSquareTexture = GenerateSVSquareTexture(_squareSize, _squareSize, _hue);
                _svSquareElement.style.backgroundImage = new StyleBackground(_svSquareTexture);

                UpdateHueCursor();
                OnColorChanged();
            }
        }

        private void OnSVSquarePointerDown(PointerDownEvent evt)
        {
            _isSVDragging = true;
            UpdateSaturationValue(evt.localPosition);
            _svSquareElement.CapturePointer(evt.pointerId);
        }

        private void OnSVSquarePointerMove(PointerMoveEvent evt)
        {
            if (_isSVDragging)
            {
                UpdateSaturationValue(evt.localPosition);
            }
        }

        private void OnSVSquarePointerUp(PointerUpEvent evt)
        {
            _isSVDragging = false;
            _svSquareElement.ReleasePointer(evt.pointerId);
        }

        private void UpdateSaturationValue(Vector2 localPosition)
        {
            float x = Mathf.Clamp(localPosition.x, 0, _squareSize);
            float y = Mathf.Clamp(localPosition.y, 0, _squareSize);

            _saturation = x / _squareSize;
            _value = 1f - y / _squareSize;

            UpdateSVCursor();
            OnColorChanged();
        }


        private void OnColorChanged()
        {
            Color selectedColor = Color.HSVToRGB(_hue, _saturation, _value);
            OnColorSelected?.Invoke(selectedColor);
        }

        private void UpdateHueCursor()
        {
            float angle = _hue * Mathf.PI * 2f;

            float radius = _textureSize / 2f - _ringThickness / 2f;
            Vector2 center = new(_textureSize / 2f, _textureSize / 2f);
    
            Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            _hueCursor.style.left = position.x - (_hueCursor.resolvedStyle.width / 2f);
            _hueCursor.style.top = position.y - (_hueCursor.resolvedStyle.height / 2f);
        }


        private void UpdateSVCursor()
        {
            float x = _saturation * _squareSize;
            float y = (1f - _value) * _squareSize;

            Vector2 position = new(x, y);

            float svLeft = (_textureSize - _squareSize) / 2f;
            float svTop = (_textureSize - _squareSize) / 2f;

            position += new Vector2(svLeft, svTop);

            _svCursor.style.left = position.x - (_svCursor.resolvedStyle.width / 2f);
            _svCursor.style.top = position.y - (_svCursor.resolvedStyle.height / 2f);
        }

        public void SetColor(Color color)
        {
            Color.RGBToHSV(color, out _hue, out _saturation, out _value);

            _svSquareTexture = GenerateSVSquareTexture(_squareSize, _squareSize, _hue);
            _svSquareElement.style.backgroundImage = new StyleBackground(_svSquareTexture);

            schedule.Execute(() =>
            {
                UpdateHueCursor();
                UpdateSVCursor();
            }).ExecuteLater(0);
        }

    }
}
