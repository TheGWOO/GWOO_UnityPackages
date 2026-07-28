using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWOO.Editor.Tools
{
    internal sealed class MaterialQueryService
    {
        public MaterialQueryResult QueryMaterials(Shader sourceShader, MaterialSearchScope scope, string folderPath)
        {
            List<MaterialListItem> items = new();
            if (sourceShader == null)
            {
                return new MaterialQueryResult(items, null, 0);
            }

            string shaderPath = AssetDatabase.GetAssetPath(sourceShader);
            Material sourceRootMaterial = string.IsNullOrEmpty(shaderPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Material>(shaderPath);

            HashSet<Material> seen = new();
            int variantChildrenCount = 0;

            if (scope == MaterialSearchScope.Scene)
            {
                QuerySceneMaterials(sourceShader, sourceRootMaterial, seen, items, ref variantChildrenCount);
            }
            else
            {
                QueryProjectMaterials(sourceShader, sourceRootMaterial, folderPath, seen, items, ref variantChildrenCount);
            }

            return new MaterialQueryResult(items, sourceRootMaterial, variantChildrenCount);
        }

        private static void QuerySceneMaterials(
            Shader sourceShader,
            Material sourceRootMaterial,
            HashSet<Material> seen,
            List<MaterialListItem> items,
            ref int variantChildrenCount)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        TryAddMaterial(
                            material,
                            sourceShader,
                            sourceRootMaterial,
                            seen,
                            items,
                            ref variantChildrenCount);
                    }
                }
            }
        }

        private static void QueryProjectMaterials(
            Shader sourceShader,
            Material sourceRootMaterial,
            string folderPath,
            HashSet<Material> seen,
            List<MaterialListItem> items,
            ref int variantChildrenCount)
        {
            string searchPath = string.IsNullOrWhiteSpace(folderPath)
                ? MaterialManagerState.DEFAULT_FOLDER_PATH
                : folderPath;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { searchPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                TryAddMaterial(
                    material,
                    sourceShader,
                    sourceRootMaterial,
                    seen,
                    items,
                    ref variantChildrenCount);
            }
        }

        private static void TryAddMaterial(
            Material material,
            Shader sourceShader,
            Material sourceRootMaterial,
            HashSet<Material> seen,
            List<MaterialListItem> items,
            ref int variantChildrenCount)
        {
            if (material == null
                || material.shader != sourceShader
                || material == sourceRootMaterial
                || !seen.Add(material))
            {
                return;
            }

            if (material.isVariant && material.parent != sourceRootMaterial)
            {
                variantChildrenCount++;
            }

            items.Add(new MaterialListItem(material));
        }
    }
}
