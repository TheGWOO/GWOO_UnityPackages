using System.Collections.Generic;
using UnityEngine;

namespace GWOO.Editor.Tools
{
    internal sealed class MaterialManagerState
    {
        public const string DEFAULT_FOLDER_PATH = "Assets";

        public Shader sourceShader;
        public Shader targetShader;
        public Material sourceRootMaterial;

        public MaterialSearchScope searchScope = MaterialSearchScope.Folder;
        public string folderPath = DEFAULT_FOLDER_PATH;
        public string searchQuery = string.Empty;

        public bool showVariants = true;
        public bool revertVisibleOnly;
        public bool reparentVisibleOnly;
        public bool showAllProperties;
        public bool actionsPanelVisible = false;

        public string filterPropertyName;
        public string filterPropertyDisplayName;

        public readonly List<MaterialListItem> foundMaterials = new();
        public readonly List<MaterialListItem> visibleMaterials = new();
        public readonly Dictionary<string, string> rebindMap = new();

        public int variantChildrenCount;
        public int materialsReparentedCount;
        public int propertiesRevertedCount;

        public string actionSummaryMessage = "Ready. No action executed yet.";
        public MaterialLogType actionSummaryType = MaterialLogType.Info;

        public string lastLogMessage = "Ready.";
        public MaterialLogType lastLogType = MaterialLogType.Info;
        public float lastLogDurationSeconds;
        public int lastLogSequence;
    }
}
