using UnityEngine.UIElements;   
using GWOO.UIElements;
using UnityEditor;
using UnityEngine;

namespace GWOO.Editor.Utils
{
    public static class EditorCustomStyles
    {
        private const string STYLES_PATH = "Styles/";
        private const string PALETTES_PATH = STYLES_PATH + "Palettes/";
        private const string DEFAULT_STYLES = "CustomStyles";

        public static void SetCustomStyleSheet(VisualElement root, Palette? palette = null)
        {
            AddStyleSheet(root, STYLES_PATH + DEFAULT_STYLES);
            
            if (palette != null)
            {
                SetPalette(root, palette.Value);
            }
        }

        public static void SetCustomTheme(VisualElement root, Palette? palette = null)
        {
            root.AddToClassList("root");
            if (palette != null)
            {
                SetPalette(root, palette.Value);
            }
            else
            {
                SetPalette(root, GetDefaultPalette());
            }
        }

        public static Palette GetDefaultPalette()
        {
            return (Palette)EditorPrefs.GetInt(ThemeMenu.SelectedThemeKey, (int)Palette.Default);
        }

        public static void SetPalette(VisualElement root, Palette newPalette)
        {
            switch (newPalette)
            {
                case Palette.Default: 
                default:
                    break;
                case Palette.Dark: 
                    AddStyleSheet(root, PALETTES_PATH + "CustomDarkPalette");
                    AddStyleSheet(root, PALETTES_PATH + "CustomVariables");
                    break;
                case Palette.Oceanic:
                    AddStyleSheet(root, PALETTES_PATH + "CustomOceanicPalette");
                    AddStyleSheet(root, PALETTES_PATH + "CustomVariables");
                    break;
            }
        }

        public static void AddStyleSheet(VisualElement root, string styleSheetPath)
        {
            StyleSheet styleSheet = Resources.Load<StyleSheet>(styleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
        }
    }
}