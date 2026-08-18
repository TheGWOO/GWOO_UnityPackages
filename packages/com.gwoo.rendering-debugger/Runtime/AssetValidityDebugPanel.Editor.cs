#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace GWOO.Development.RenderingDebugger
{
    internal static partial class AssetValidityDebugPanel
    {
        #region EditorHooks
        private sealed class EditorHooks : IHooks
        {
            public void Repaint()
            {
                if (DebugManager.instance != null)
                {
                    DebugManager.instance.ReDrawOnScreenDebug();
                }
                
                SceneView.RepaintAll();
            }

            public void AugmentCameraList(List<Camera> list, Camera main)
            {
                Camera sceneCam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
                
                if (sceneCam == null)
                    return;
                
                if (sceneCam.cameraType == CameraType.Preview || sceneCam.cameraType == CameraType.Reflection)
                    return;
                
                if (!list.Contains(sceneCam))
                {
                    list.Add(sceneCam);
                }
            }

            public string GetCameraDisplayName(Camera cam, Camera main)
            {
                bool isSceneView = SceneView.lastActiveSceneView != null && cam == SceneView.lastActiveSceneView.camera;
                string name = isSceneView ? "Scene View" : cam.name;
                
                if (cam == main)
                {
                    name += " (Main)";
                }

                return name;
            }

            public bool TryDrawVisuals(Camera cam, string label, Bounds worldBounds, int sourceTexMaxDimPx, float screenPx, float ratio, Color color)
            {
                if (_showBoxes)
                {
                    Handles.color = color;
                    Handles.DrawWireCube(worldBounds.center, worldBounds.size);
                }

                if (_showLabels)
                {
                    DrawLabel(cam, worldBounds.center, label, screenPx, sourceTexMaxDimPx, ratio, color);
                }

                if (_showLegend)
                {
                    DrawLegendOnce(cam);
                }

                return true;
            }

            public void WireEditorEvents()
            {
                UnwireEditorEvents();
                EditorApplication.hierarchyChanged += OnHierarchyChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                EditorApplication.quitting += OnEditorQuitting;
                AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            }

            public void UnwireEditorEvents()
            {
                EditorApplication.hierarchyChanged -= OnHierarchyChanged;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.quitting -= OnEditorQuitting;
                AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            }
        }

        static AssetValidityDebugPanel()
        {
            HOOKS = new EditorHooks();
            ASSET_COLUMN = new ObjectFieldAssetColumn();
        }
        #endregion EditorHooks

        #region EditorAssetColumn
        private sealed class ObjectFieldAssetColumn : IAssetColumn
        {
            public void AddHeader(DebugUI.Table table)
            {
                ((DebugUI.Table.Row)table.children[0]).children.Add(new DebugUI.Value { displayName = "Asset", getter = () => string.Empty });
            }

            public void AddCells(DebugUI.Table table, List<Result> results)
            {
                for (int i = 1; i < table.children.Count; i++)
                {
                    int idx = i - 1;
                    if (idx >= results.Count)
                    {
                        ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.Value { displayName = string.Empty, getter = () => string.Empty });
                        continue;
                    }

                    EntityId idLocal = results[idx].instanceId;
                    ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.ObjectField
                    {
                        displayName = string.Empty,
                        getter = () => EditorUtility.EntityIdToObject(idLocal),
                        setter = o =>
                        {
                            Object target = o != null ? o : EditorUtility.EntityIdToObject(idLocal);
                            if (target == null) return;
                            Selection.activeObject = target;
                            EditorGUIUtility.PingObject(target);
                        }
                    });
                }
            }
        }
        #endregion EditorAssetColumn

        #region Gizmos
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
        private static void DrawForMeshRenderer(MeshRenderer meshRenderer, GizmoType gizmo)
        {
            if (meshRenderer == null || !meshRenderer.enabled)
                return;
            
            if (_checkType == 0 || (_rendererMask & RendererTypes.MeshRenderer) == 0)
                return;

            Material material = meshRenderer.sharedMaterial;
            
            if (material == null || material.shader == null)
                return;

            Texture mainTex = TryGetMainTex(material);
            
            if (mainTex == null)
                return;

            int texMax = Mathf.Max(mainTex.width, mainTex.height);
            if (!TryComputeVisualInfo(
                    meshRenderer.gameObject.name,
                    meshRenderer.bounds, texMax,
                    out Camera cam,
                    out float screenPx,
                    out float ratio,
                    out Color color))
                return;

            HOOKS.TryDrawVisuals(cam, meshRenderer.gameObject.name, meshRenderer.bounds, texMax, screenPx, ratio, color);
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
        private static void DrawForSpriteRenderer(SpriteRenderer spriteRenderer, GizmoType gizmo)
        {
            if (spriteRenderer == null || !spriteRenderer.enabled || spriteRenderer.sprite == null) return;
            if (_checkType == 0 || (_rendererMask & RendererTypes.SpriteRenderer) == 0) return;

            int texMax = Mathf.Max((int)spriteRenderer.sprite.rect.width, (int)spriteRenderer.sprite.rect.height);
            
            if (!TryComputeVisualInfo(
                    spriteRenderer.gameObject.name,
                    spriteRenderer.bounds,
                    texMax,
                    out Camera cam,
                    out float screenPx,
                    out float ratio,
                    out Color color))
                return;

            HOOKS.TryDrawVisuals(cam, spriteRenderer.gameObject.name, spriteRenderer.bounds, texMax, screenPx, ratio, color);
        }
        #endregion Gizmos

        #region EditorUI
        private static GUIStyle _labelStyle;
        private static readonly GUIContent LABEL_CONTENT = new();
        private static Texture2D _legendTex;
        private static GUIStyle _legendTitleStyle;
        private static GUIStyle _legendSmallStyle;
        private static GUIStyle _legendExplanationStyle;
        private static readonly HashSet<int> LEGEND_DRAWN_CAMERAS = new();
        private static int _legendDrawFrame = -1;

        private static void DrawLabel(Camera camera, Vector3 worldPos, string label, float screenPx, int texels, float ratio, Color color)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = Mathf.Max(MIN_FONT_SIZE, _fontSize),
                    richText = true,
                    wordWrap = false,
                    alignment = TextAnchor.UpperLeft
                };
            }

            int small = Mathf.Max(8, _fontSize - 4);
            LABEL_CONTENT.text = $"<b>{label}</b>\n<size={small}>Screen: {screenPx:F0}px</size>\n<size={small}>Texels: {texels}px</size>\n<size={small}>Ratio: {ratio:F2}</size>";

            _labelStyle.fontSize = Mathf.Max(MIN_FONT_SIZE, _fontSize);
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.wordWrap = true;
            _labelStyle.normal.textColor = color;

            Vector3 sp = camera.WorldToScreenPoint(worldPos);
            bool sceneView = SceneView.currentDrawingSceneView != null && Camera.current == SceneView.currentDrawingSceneView.camera;

            if (sceneView)
            {
                Handles.Label(worldPos, LABEL_CONTENT, _labelStyle);
                return;
            }

            if (sp.z <= 0f)
                return;

            Handles.BeginGUI();
            Matrix4x4 prev = GUI.matrix;
            ComputeViewMapping(camera, Screen.width, Screen.height, out float scale, out float offX, out float offY);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX, offY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            Vector2 size = _labelStyle.CalcSize(LABEL_CONTENT);
            Rect r = new(sp.x - size.x * 0.5f, camera.pixelHeight - sp.y - size.y * 0.5f, size.x + 10f, size.y + 6f);
            GUI.Label(r, LABEL_CONTENT, _labelStyle);

            GUI.matrix = prev;
            Handles.EndGUI();
        }

        private static void ComputeViewMapping(Camera camera, int viewW, int viewH, out float scale, out float offX, out float offY)
        {
            int rw = Mathf.Max(1, camera.pixelWidth);
            int rh = Mathf.Max(1, camera.pixelHeight);
            float sx = (float)viewW / rw;
            float sy = (float)viewH / rh;
            scale = Math.Min(sx, sy);
            offX = (viewW - rw * scale) * 0.5f;
            offY = (viewH - rh * scale) * 0.5f;
        }

        private static void DrawLegendOnce(Camera camera)
        {
            int frame = Time.renderedFrameCount;
            if (_legendDrawFrame != frame)
            {
                _legendDrawFrame = frame;
                LEGEND_DRAWN_CAMERAS.Clear();
            }

            int cameraId = camera != null ? camera.GetInstanceID() : 0;
            if (!LEGEND_DRAWN_CAMERAS.Add(cameraId))
                return;

            DrawLegend(camera);
        }

        private static void DrawLegend(Camera camera)
        {
            if (camera == null)
                return;

            if (_legendTex == null || _legendTex.width != LEGEND_TEX_W || _legendTex.height != LEGEND_TEX_H)
            {
                DisposeLegend();
                _legendTex = new Texture2D(LEGEND_TEX_W, LEGEND_TEX_H, TextureFormat.RGBA32, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                for (int x = 0; x < LEGEND_TEX_W; x++)
                {
                    float t = x / (float)(LEGEND_TEX_W - 1);
                    Color leftRed = Color.red;
                    Color midGreen = Color.green;
                    Color rightBlue = new(0.05f, 0.2f, 0.9f);
                    Color col = t <= 0.5f ? Color.Lerp(leftRed, midGreen, t / 0.5f) : Color.Lerp(midGreen, rightBlue, (t - 0.5f) / 0.5f);
                    _legendTex.SetPixel(x, 0, col);
                }

                _legendTex.Apply(false, false);
            }

            EnsureLegendStyles();

            Handles.BeginGUI();
            Matrix4x4 prev = GUI.matrix;
            ComputeViewMapping(camera, Screen.width, Screen.height, out float scale, out float offX, out float offY);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX, offY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            float w = Mathf.Clamp(camera.pixelWidth * LEGEND_WIDTH_PCT, LEGEND_MIN_W, LEGEND_MAX_W);
            float h = Mathf.Clamp(camera.pixelHeight * LEGEND_HEIGHT_PCT, LEGEND_MIN_H, LEGEND_MAX_H);
            Rect box = new(LEGEND_MARGIN_PX, camera.pixelHeight - h - LEGEND_MARGIN_PX, w, h);

            float pad = 10f;
            Rect grad = new(box.x + pad, box.y + pad + 18f, box.width - pad * 2f, 24f);
            Rect labels = new(box.x + pad, grad.yMax + 6f, box.width - pad * 2f, box.height - (grad.yMax - box.y) - 10f);

            GUI.Box(box, GUIContent.none);

            GUI.Label(new Rect(box.x + pad, box.y + 4f, box.width - pad * 2f, 20f), "Texel Density (Under ↔ Target ↔ Over)", _legendTitleStyle);

            GUI.DrawTexture(grad, _legendTex, ScaleMode.StretchToFill);

            GUI.Label(new Rect(grad.x, grad.yMax + 2f, 80f, 16f), "Under", _legendSmallStyle);
            GUI.Label(new Rect(grad.x + grad.width * 0.48f - 20f, grad.yMax + 2f, 80f, 16f), "Target", _legendSmallStyle);
            GUI.Label(new Rect(grad.x + grad.width - 70f, grad.yMax + 2f, 80f, 16f), "Over", _legendSmallStyle);

            GUI.Label(labels, $"<color=#AAAAAA>Good:</color> ±{_goodTolerance:P0}  <color=#AAAAAA>Almost:</color> ±{_almostTolerance:P0}  <color=#AAAAAA>Bad:</color> outside", _legendExplanationStyle);

            GUI.matrix = prev;
            Handles.EndGUI();
        }

        private static void EnsureLegendStyles()
        {
            if (_legendTitleStyle == null)
            {
                _legendTitleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperLeft };
                _legendTitleStyle.normal.textColor = Color.white;
            }

            if (_legendSmallStyle == null)
            {
                _legendSmallStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.UpperLeft };
                _legendSmallStyle.normal.textColor = Color.white;
            }

            if (_legendExplanationStyle == null)
            {
                _legendExplanationStyle = new GUIStyle(EditorStyles.label) { richText = true };
                _legendExplanationStyle.normal.textColor = Color.white;
            }
        }

        private static void DisposeLegend()
        {
            if (_legendTex == null)
                return;
            
            Object.DestroyImmediate(_legendTex);
            _legendTex = null;
        }
        #endregion EditorUI

        #region EditorEvents
        private static void OnHierarchyChanged()
        {
            if (RefreshCamerasIfChanged())
            {
                RebuildPanel();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (RefreshCamerasIfChanged())
            {
                RebuildPanel();
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            ShutdownEditorResources();
        }

        private static void OnEditorQuitting()
        {
            ShutdownEditorResources();
        }

        private static void ShutdownEditorResources()
        {
            HOOKS.UnwireEditorEvents();
            if (_panel != null && DebugManager.instance != null)
            {
                DebugManager.instance.RemovePanel(PANEL_NAME);
            }

            _panel = null;
            DisposeLegend();
        }
        #endregion EditorEvents
    }
}
#endif
