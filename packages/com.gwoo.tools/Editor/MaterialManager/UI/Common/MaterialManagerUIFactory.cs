using System;
using GWOO.Editor.Utils;
using GWOO.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    internal static class MaterialManagerUIFactory
    {
        private const string BASE_STYLESHEET_PATH = "Styles/MaterialManagerStyle";
        private const string LAYOUT_STYLESHEET_PATH = "Styles/MaterialManagerLayoutStyle";
        private const int SEPARATOR_THICKNESS = 2;
        private const string SEPARATOR_VARIANT = "spaced";

        public static void ApplyRootThemeAndStyles(VisualElement root)
        {
            if (root == null)
            {
                throw new InvalidOperationException("Cannot apply Material Manager styles because the root element is null.");
            }

            EditorCustomStyles.SetCustomStyleSheet(root);
            EditorCustomStyles.SetCustomTheme(root);

            AddStyleSheet(root, BASE_STYLESHEET_PATH);
            AddStyleSheet(root, LAYOUT_STYLESHEET_PATH);
        }

        public static VisualElement CreateSeparator()
        {
            return new Separator(SEPARATOR_THICKNESS, SEPARATOR_VARIANT);
        }

        public static VisualElement BuildPopupRoot(VisualElement rootVisualElement)
        {
            if (rootVisualElement == null)
            {
                throw new InvalidOperationException("Cannot build popup root because the root element is null.");
            }

            rootVisualElement.Clear();
            ApplyRootThemeAndStyles(rootVisualElement);

            VisualElement root = new();
            root.AddToClassList("mm-root");
            root.AddToClassList("mm-popup-root");
            rootVisualElement.Add(root);

            return root;
        }

        public static T RequireElement<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root?.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException($"Missing required UI element '{name}' ({typeof(T).Name}).");
            }

            return element;
        }

        private static void AddStyleSheet(VisualElement root, string path)
        {
            StyleSheet style = Resources.Load<StyleSheet>(path);
            if (style != null && !root.styleSheets.Contains(style))
            {
                root.styleSheets.Add(style);
            }
        }
    }
}
