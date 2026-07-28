using UnityEditor;
using GWOO.UIElements;

namespace GWOO.Editor.Utils
{
    public static class ThemeMenu
    {
        public const string SelectedThemeKey = "ThemeManager_Palette";
        private const string __menuPath = "Tools/GWOO/Editor UI/Theme/";

        [MenuItem(__menuPath + "Default", false, 1)]
        public static void SetDefaultTheme()
        {
            SetTheme(Palette.Default);
        }

        [MenuItem(__menuPath + "Dark", false, 2)]
        public static void SetDarkTheme()
        {
            SetTheme(Palette.Dark);
        }

        [MenuItem(__menuPath + "Oceanic", false, 3)]
        public static void SetOceanicTheme()
        {
            SetTheme(Palette.Oceanic);
        }
        
        private static void SetTheme(Palette theme)
        {
            EditorPrefs.SetInt(SelectedThemeKey, (int)theme);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }


        // Check Default Theme
        [MenuItem(__menuPath + "Default", true)]
        public static bool CheckDefaultTheme()
        {
            Menu.SetChecked(__menuPath + "Default", EditorPrefs.GetInt(SelectedThemeKey, (int)Palette.Default) == (int)Palette.Default);
            return true;
        }

        // Check Dark Theme
        [MenuItem(__menuPath + "Dark", true)]
        public static bool CheckDarkTheme()
        {
            Menu.SetChecked(__menuPath + "Dark", EditorPrefs.GetInt(SelectedThemeKey, (int)Palette.Dark) == (int)Palette.Dark);
            return true;
        }

        // Check Oceanic Theme
        [MenuItem(__menuPath + "Oceanic", true)]
        public static bool CheckOceanicTheme()
        {
            Menu.SetChecked(__menuPath + "Oceanic", EditorPrefs.GetInt(SelectedThemeKey, (int)Palette.Oceanic) == (int)Palette.Oceanic);
            return true;
        }
    }
}