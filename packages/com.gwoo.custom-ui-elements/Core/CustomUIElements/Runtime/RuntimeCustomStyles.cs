using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public static class RuntimeCustomStyles
    {
        private static StyleSheet _styles;
        public static StyleSheet Styles => _styles;
        
        private static StyleSheet _palette;
        public static StyleSheet Palette => _palette;
        
        public static void SetCustomStyleSheet(VisualElement root, StyleSheet palette, bool backgroundColor = false)
        {
            if (_styles != null)
            {
                AddStyleSheet(root, _styles);
            }

            if (_palette != null)
            {
                AddStyleSheet(root, _palette);
            }

            if (backgroundColor)
            {
                root.AddToClassList("custom-background");
            }
        }
        
        public static void AddStyleSheet(VisualElement root, StyleSheet style)
        {
            if (style != null)
            {
                root.styleSheets.Add(style);
            }
        }

        public static void SetStyleValue(StyleSheet style)
        {
            _styles = style;
        }

        public static void SetPaletteValue(StyleSheet palette)
        {
            _palette = palette;
        }
    }
}