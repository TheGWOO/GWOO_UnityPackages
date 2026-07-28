using UnityEditor;

namespace GWOO.Editor.Tools
{
    internal sealed class MaterialManagerSettingsService
    {
        private const string PREFIX = "MaterialManager.";
        private const string SOURCE_SHADER_KEY = PREFIX + "SourceShader";
        private const string TARGET_SHADER_KEY = PREFIX + "TargetShader";
        private const string FOLDER_PATH_KEY = PREFIX + "FolderPath";
        private const string SCOPE_KEY = PREFIX + "SearchScope";
        private const string SHOW_VARIANTS_KEY = PREFIX + "ShowVariants";
        private const string REVERT_VISIBLE_ONLY_KEY = PREFIX + "RevertVisibleOnly";
        private const string REPARENT_VISIBLE_ONLY_KEY = PREFIX + "ReparentVisibleOnly";
        private const string SHOW_ALL_PROPERTIES_KEY = PREFIX + "ShowAllProperties";
        private const string ACTIONS_PANEL_VISIBLE_KEY = PREFIX + "ActionsPanelVisible";
        private const string LEGACY_OPERATIONS_PANEL_AUTO_HIDE_KEY = PREFIX + "ActionsPanelAutoHide";

        public void Save(MaterialManagerState state)
        {
            if (state == null)
            {
                return;
            }

            string sourceShaderPath = state.sourceShader != null
                ? AssetDatabase.GetAssetPath(state.sourceShader)
                : string.Empty;
            string targetShaderPath = state.targetShader != null
                ? AssetDatabase.GetAssetPath(state.targetShader)
                : string.Empty;

            EditorPrefs.SetString(SOURCE_SHADER_KEY, sourceShaderPath);
            EditorPrefs.SetString(TARGET_SHADER_KEY, targetShaderPath);
            EditorPrefs.SetString(FOLDER_PATH_KEY, state.folderPath);

            EditorPrefs.SetInt(SCOPE_KEY, (int)state.searchScope);
            EditorPrefs.SetBool(SHOW_VARIANTS_KEY, state.showVariants);
            EditorPrefs.SetBool(REVERT_VISIBLE_ONLY_KEY, state.revertVisibleOnly);
            EditorPrefs.SetBool(REPARENT_VISIBLE_ONLY_KEY, state.reparentVisibleOnly);
            EditorPrefs.SetBool(SHOW_ALL_PROPERTIES_KEY, state.showAllProperties);
            EditorPrefs.SetBool(ACTIONS_PANEL_VISIBLE_KEY, state.actionsPanelVisible);
        }

        public void Load(MaterialManagerState state)
        {
            if (state == null)
            {
                return;
            }

            string migratedSourceShaderPath = EditorPrefs.GetString("ShaderManager_shaderField.value", string.Empty);
            string sourceShaderPath = EditorPrefs.GetString(SOURCE_SHADER_KEY, migratedSourceShaderPath);
            string targetShaderPath = EditorPrefs.GetString(TARGET_SHADER_KEY, string.Empty);

            state.sourceShader = AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(sourceShaderPath);
            state.targetShader = AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(targetShaderPath);

            string migratedFolderPath = EditorPrefs.GetString("ShaderManager_SearchFolder", MaterialManagerState.DEFAULT_FOLDER_PATH);
            state.folderPath = EditorPrefs.GetString(FOLDER_PATH_KEY, migratedFolderPath);

            int migratedScope = EditorPrefs.GetInt("ShaderManager_SearchInOption", (int)MaterialSearchScope.Folder);
            int scopeValue = EditorPrefs.GetInt(SCOPE_KEY, migratedScope);
            state.searchScope = scopeValue == (int)MaterialSearchScope.Scene
                ? MaterialSearchScope.Scene
                : MaterialSearchScope.Folder;

            bool migratedShowVariants = EditorPrefs.GetBool("ShaderManager_FilterState", true);
            state.showVariants = EditorPrefs.GetBool(SHOW_VARIANTS_KEY, migratedShowVariants);
            state.revertVisibleOnly = EditorPrefs.GetBool(REVERT_VISIBLE_ONLY_KEY, false);
            state.reparentVisibleOnly = EditorPrefs.GetBool(REPARENT_VISIBLE_ONLY_KEY, false);
            state.showAllProperties = EditorPrefs.GetBool(SHOW_ALL_PROPERTIES_KEY, false);

            if (EditorPrefs.HasKey(ACTIONS_PANEL_VISIBLE_KEY))
            {
                state.actionsPanelVisible = EditorPrefs.GetBool(ACTIONS_PANEL_VISIBLE_KEY, true);
            }
            else if (EditorPrefs.HasKey(LEGACY_OPERATIONS_PANEL_AUTO_HIDE_KEY))
            {
                state.actionsPanelVisible = true;
            }
            else
            {
                state.actionsPanelVisible = true;
            }
        }
    }
}
