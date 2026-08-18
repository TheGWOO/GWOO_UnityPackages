using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace GWOO.Development.RenderingDebugger
{
    internal static partial class AssetValidityDebugPanel
    {
        #region Constants
        private const string PANEL_NAME = "Asset Validity";
        private const float MIN_TARGET_RATIO = 0.001f;
        private const int MIN_FONT_SIZE = 10;

        private const int LEGEND_TEX_W = 256;
        private const int LEGEND_TEX_H = 1;
        private const int LEGEND_MIN_W = 220;
        private const int LEGEND_MAX_W = 600;
        private const int LEGEND_MIN_H = 70;
        private const int LEGEND_MAX_H = 180;
        private const float LEGEND_WIDTH_PCT = 0.30f;
        private const float LEGEND_HEIGHT_PCT = 0.15f;
        private const int LEGEND_MARGIN_PX = 12;

        private const int MAX_SCAN_BUFFER = 16384;
        private const int RESULTS_DEFAULT_MAX_ROWS = 500;
        #endregion Constants

        #region EnumsAndTypes
        private enum ValidityCheckType { None, TexelDensity }
        [Flags] private enum SeverityMask { None = 0, Bad = 1 << 0, Almost = 1 << 1, Good = 1 << 2 }
        [Flags] private enum RendererTypes { None = 0, MeshRenderer = 1 << 0, SpriteRenderer = 1 << 1 }
        private enum Severity { Bad, Almost, Good }
        private enum SortColumn { Asset, Severity, Ratio, ScreenPx, Texels }
        [Flags] private enum TargetDirectionMask { None = 0, UnderTarget = 1 << 0, OverTarget = 1 << 1 }
        private enum TargetDirection { UnderTarget, OverTarget }

        private sealed class Result
        {
            public EntityId instanceId;
            public string name;
            public float ratio;
            public float screenPx;
            public int texels;
        }
        #endregion EnumsAndTypes

        #region State
        private static DebugUI.Panel _panel;
        private static DebugUI.Table _resultsTable;

        private static Camera _mainCamera;
        private static Camera _visualReferenceCamera;
        private static int _selectedCameraIndex = -1;
        private static Camera[] _camerasArray;
        private static readonly List<Camera> CAMERAS = new(16);
        private static GUIContent[] _cameraGuiNames;
        private static int[] _cameraValues;
        private static int _selectedCamInstanceId;
        private static int _camerasSignature;
        private static int _visualReferenceFrame = -1;

        private static ValidityCheckType _checkType = ValidityCheckType.None;

        private static RendererTypes _rendererMask = RendererTypes.MeshRenderer | RendererTypes.SpriteRenderer;
        private static SeverityMask _severityMask = SeverityMask.Bad | SeverityMask.Almost | SeverityMask.Good;
        private static TargetDirectionMask _targetDirectionMask = TargetDirectionMask.UnderTarget | TargetDirectionMask.OverTarget;

        private static float _targetRatio = 1f;
        private static float _goodTolerance = 0.10f;
        private static float _almostTolerance = 0.25f;

        private static bool _showBoxes = true;
        private static bool _showLabels = true;
        private static bool _showLegend = true;
        private static int _fontSize = 16;

        private static readonly List<Result> ALL_RESULTS = new(1024);
        private static readonly List<Result> VIEW_RESULTS = new(1024);

        private static readonly List<MeshRenderer> SCAN_MESHES = new(2048);
        private static readonly List<SpriteRenderer> SCAN_SPRITES = new(2048);
        private static readonly HashSet<EntityId> SEEN_RESULTS = new();
        private static readonly Vector3[] BOUNDS_CORNERS = new Vector3[8];

        private const SortColumn SORT_COLUMN = SortColumn.Severity;
        private const bool SORT_ASCENDING = true;
        private static int _resultsMaxRows = RESULTS_DEFAULT_MAX_ROWS;
        #endregion State

        #region InterfacesAndHooks
        private interface IAssetColumn
        {
            void AddHeader(DebugUI.Table table);
            void AddCells(DebugUI.Table table, List<Result> results);
        }

        private sealed class NameAssetColumn : IAssetColumn
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

                    string nm = results[idx].name;
                    ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.Value { displayName = string.Empty, getter = () => nm });
                }
            }
        }

        private interface IHooks
        {
            void Repaint();
            void AugmentCameraList(List<Camera> list, Camera main);
            string GetCameraDisplayName(Camera cam, Camera main);
            bool TryDrawVisuals(Camera cam, string label, Bounds worldBounds, int sourceTexMaxDimPx, float screenPx, float ratio, Color color);
            void WireEditorEvents();
            void UnwireEditorEvents();
        }

        private sealed class RuntimeHooks : IHooks
        {
            public void Repaint() { }
            public void AugmentCameraList(List<Camera> list, Camera main) { }
            public string GetCameraDisplayName(Camera cam, Camera main) => cam == main ? $"{cam.name} (Main)" : cam.name;
            public bool TryDrawVisuals(Camera cam, string label, Bounds worldBounds, int sourceTexMaxDimPx, float screenPx, float ratio, Color color) => false;
            public void WireEditorEvents() { }
            public void UnwireEditorEvents() { }
        }

        private static readonly IHooks HOOKS = new RuntimeHooks();
        private static readonly IAssetColumn ASSET_COLUMN = new NameAssetColumn();
        #endregion InterfacesAndHooks

        #region Initialization
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#endif
        private static void Bootstrap()
        {
            EnsurePanel();
            HOOKS.WireEditorEvents();
        }
        

        private static void EnsurePanel()
        {
            if (DebugManager.instance == null)
                return;
            if (_panel != null)
                return;

            _panel = DebugManager.instance.GetPanel(PANEL_NAME, true);
            RebuildPanel();
        }
        #endregion Initialization

        #region PanelBuild
        private static void RebuildPanel()
        {
            if (_panel == null)
                return;

            RefreshCamerasIfChanged();

            _panel.children.Clear();

            DebugUI.Container root = new();
            _panel.children.Add(root);

            root.children.Add(new DebugUI.EnumField
            {
                displayName = "Validation Mode",
                autoEnum = typeof(ValidityCheckType),
                getter = () => (int)_checkType,
                setter = v =>
                {
                    _checkType = (ValidityCheckType)v;
                    RebuildPanel();
                    HOOKS.Repaint();
                },
                getIndex = () => (int)_checkType,
                setIndex = v =>
                {
                    _checkType = (ValidityCheckType)v;
                    RebuildPanel();
                    HOOKS.Repaint();
                }
            });

            if (_checkType == ValidityCheckType.None)
                return;

            root.children.Add(CameraSelector());
            root.children.Add(new DebugUI.BitField
            {
                displayName = "Renderer Types",
                enumType = typeof(RendererTypes),
                getter = () => _rendererMask,
                setter = v =>
                {
                    _rendererMask = (RendererTypes)v;
                    HOOKS.Repaint();
                }
            });
            root.children.Add(new DebugUI.BitField
            {
                displayName = "Show Severities",
                enumType = typeof(SeverityMask),
                getter = () => _severityMask,
                setter = v =>
                {
                    _severityMask = (SeverityMask)v;
                    RefreshResultsView();
                    RebuildResultsTable();
                    HOOKS.Repaint();
                }
            });
            root.children.Add(new DebugUI.BitField
            {
                displayName = "Direction",
                enumType = typeof(TargetDirectionMask),
                getter = () => _targetDirectionMask,
                setter = v =>
                {
                    _targetDirectionMask = (TargetDirectionMask)v;
                    RefreshResultsView();
                    RebuildResultsTable();
                    HOOKS.Repaint();
                }
            });

            if (_checkType == ValidityCheckType.TexelDensity)
            {
                root.children.Add(new DebugUI.FloatField
                {
                    displayName = "Target Ratio (texel/pixel)",
                    getter = () => _targetRatio,
                    setter = v =>
                    {
                        _targetRatio = Mathf.Max(MIN_TARGET_RATIO, v);
                        RefreshResultsView();
                        RebuildResultsTable();
                        HOOKS.Repaint();
                    },
                    incStep = 0.1f,
                    incStepMult = 10f
                });
                root.children.Add(new DebugUI.FloatField
                {
                    displayName = "Good ± Tolerance",
                    getter = () => _goodTolerance,
                    setter = v =>
                    {
                        _goodTolerance = Mathf.Clamp(v, 0.01f, 0.90f);
                        if (_almostTolerance < _goodTolerance) _almostTolerance = Mathf.Clamp(_goodTolerance, 0.02f, 0.95f);
                        RefreshResultsView();
                        RebuildResultsTable();
                        HOOKS.Repaint();
                    },
                    incStep = 0.01f,
                    incStepMult = 10f
                });
                root.children.Add(new DebugUI.FloatField
                {
                    displayName = "Almost ± Tolerance",
                    getter = () => _almostTolerance,
                    setter = v =>
                    {
                        _almostTolerance = Mathf.Clamp(Mathf.Max(v, _goodTolerance), 0.02f, 0.95f);
                        RefreshResultsView();
                        RebuildResultsTable();
                        HOOKS.Repaint();
                    },
                    incStep = 0.01f,
                    incStepMult = 10f
                });
            }

            root.children.Add(new DebugUI.BoolField { displayName = "Show Boxes", getter = () => _showBoxes, setter = v => { _showBoxes = v; HOOKS.Repaint(); } });
            root.children.Add(new DebugUI.BoolField { displayName = "Show Labels", getter = () => _showLabels, setter = v => { _showLabels = v; HOOKS.Repaint(); } });
            root.children.Add(new DebugUI.IntField { displayName = "Font Size", getter = () => _fontSize, setter = v => { _fontSize = Mathf.Max(MIN_FONT_SIZE, v); HOOKS.Repaint(); }, incStep = 1 });
            root.children.Add(new DebugUI.BoolField { displayName = "Show Legend", getter = () => _showLegend, setter = v => { _showLegend = v; HOOKS.Repaint(); } });

            root.children.Add(new DebugUI.Button { displayName = "Rescan Scene", action = () => { ScanResults(); RebuildResultsTable(); HOOKS.Repaint(); } });
            root.children.Add(new DebugUI.Button
            {
                displayName = "Clear Results",
                action = () =>
                {
                    ALL_RESULTS.Clear();
                    VIEW_RESULTS.Clear();
                    RebuildResultsTable();
                    HOOKS.Repaint();
                },
                isHiddenCallback = () => VIEW_RESULTS.Count == 0
            });
            root.children.Add(new DebugUI.IntField
            {
                displayName = "Max Rows",
                getter = () => _resultsMaxRows,
                setter = v =>
                {
                    _resultsMaxRows = Mathf.Clamp(v, 10, MAX_SCAN_BUFFER);
                    RebuildResultsTable();
                },
                incStep = 50
            });
            root.children.Add(new DebugUI.MessageBox { displayName = "No results. Click Rescan Scene.", isHiddenCallback = () => VIEW_RESULTS.Count != 0 });

            _resultsTable = CreateResultsTable();
            _panel.children.Add(_resultsTable);
        }
        #endregion PanelBuild

        #region Camera
        private static DebugUI.Widget CameraSelector()
        {
            if (CAMERAS.Count == 0)
                return new DebugUI.Value { displayName = "Reference Camera", getter = () => "No cameras" };

            int ClampCam(int v) => Mathf.Clamp(v, 0, CAMERAS.Count - 1);

            void ApplyCamIndex(int idx)
            {
                _selectedCameraIndex = ClampCam(idx);
                _selectedCamInstanceId = CAMERAS[_selectedCameraIndex] != null ? CAMERAS[_selectedCameraIndex].GetInstanceID() : 0;
                _visualReferenceFrame = -1;
                ScanResults();
                RebuildResultsTable();
                HOOKS.Repaint();
            }

           
            return new DebugUI.EnumField
            {
                displayName = "Reference Camera",
                enumNames = _cameraGuiNames,
                enumValues = _cameraValues,
                getter = () => ClampCam(_selectedCameraIndex),
                setter = v => ApplyCamIndex(v),
                getIndex = () => ClampCam(_selectedCameraIndex),
                setIndex = v => ApplyCamIndex(v)
            };
        }

        private static void BuildCameraList()
        {
            CAMERAS.Clear();

            if (_camerasArray == null || _camerasArray.Length != Camera.allCamerasCount) _camerasArray = new Camera[Camera.allCamerasCount];
            Camera.GetAllCameras(_camerasArray);

            Camera main = Camera.main;
            
            if (main != null
                && main.cameraType != CameraType.Preview
                && main.cameraType != CameraType.Reflection
                && !CAMERAS.Contains(main))
            {
                CAMERAS.Add(main);
            }

            for (int i = 0; i < _camerasArray.Length; i++)
            {
                Camera cam = _camerasArray[i];
                if (cam == null) continue;
                if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) continue;
                if (!CAMERAS.Contains(cam)) CAMERAS.Add(cam);
            }

            HOOKS.AugmentCameraList(CAMERAS, main);

            if (CAMERAS.Count == 0)
            {
                _selectedCameraIndex = -1;
               
                return;
            }

            int found = -1;

            if (_selectedCamInstanceId != 0)
            {
                for (int i = 0; i < CAMERAS.Count; i++)
                {
                    Camera c = CAMERAS[i];
                    if (c != null && c.GetInstanceID() == _selectedCamInstanceId)
                    {
                        found = i;
                        break;
                    }
                }
            }
            else
            {
                if (main != null)
                {
                    for (int i = 0; i < CAMERAS.Count; i++)
                    {
                        if (CAMERAS[i] == main)
                        {
                            found = i;
                            break;
                        }
                    }
                }

                if (found < 0) found = 0;
            }

            _selectedCameraIndex = Mathf.Clamp(found, 0, CAMERAS.Count - 1);
            _selectedCamInstanceId = CAMERAS[_selectedCameraIndex] != null ? CAMERAS[_selectedCameraIndex].GetInstanceID() : 0;
        }

        private static int ComputeCamerasSignature()
        {
            BuildCameraList();

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + CAMERAS.Count;

                for (int i = 0; i < CAMERAS.Count; i++)
                {
                    Camera c = CAMERAS[i];
                    hash = hash * 23 + (c != null ? c.GetInstanceID() : 0);
                    hash = hash * 23 + (c != null ? c.name.GetHashCode() : 0);
                    hash = hash * 23 + (ReferenceEquals(c, Camera.main) ? 1 : 0);
                }

               
                return hash;
            }
        }

        private static bool RefreshCamerasIfChanged()
        {
            int newSig = ComputeCamerasSignature();
            if (newSig == _camerasSignature)
                return false;

            _camerasSignature = newSig;

            int count = CAMERAS.Count;
            _cameraGuiNames = new GUIContent[count];
            _cameraValues = new int[count];

            Camera main = Camera.main;

            for (int i = 0; i < count; i++)
            {
                Camera cam = CAMERAS[i];
                string name = HOOKS.GetCameraDisplayName(cam, main);
                _cameraGuiNames[i] = new GUIContent(name);
                _cameraValues[i] = i; // values == indices for robustness
            }

           
            return true;
        }

        private static Camera GetReferenceCamera()
        {
            BuildCameraList();

            if (_selectedCameraIndex >= 0 && _selectedCameraIndex < CAMERAS.Count)
            {
                Camera selected = CAMERAS[_selectedCameraIndex];
                if (selected != null)
                    return selected;
            }

            if (_mainCamera == null || !_mainCamera) _mainCamera = Camera.main;
           
            return _mainCamera;
        }

        private static Camera GetVisualReferenceCamera()
        {
            int frame = Time.renderedFrameCount;
            if (_visualReferenceFrame != frame || _visualReferenceCamera == null)
            {
                _visualReferenceFrame = frame;
                _visualReferenceCamera = GetReferenceCamera();
            }

            return _visualReferenceCamera;
        }
        #endregion Camera

        #region ResultsTable
        private static DebugUI.Table CreateResultsTable()
        {
            DebugUI.Table table = new()
            {
                displayName = "Density",
                isReadOnly = true,
                isHiddenCallback = () => VIEW_RESULTS.Count == 0
            };

            GenerateResultsRows(table);
            GenerateResultsColumns(table);
           
            return table;
        }

        private static void RebuildResultsTable()
        {
            if (_panel == null)
                return;
            if (_resultsTable != null) _panel.children.Remove(_resultsTable);
            _resultsTable = CreateResultsTable();
            _panel.children.Add(_resultsTable);
        }

        private static void GenerateResultsRows(DebugUI.Table table)
        {
            table.children.Clear();
            table.children.Add(new DebugUI.Table.Row { displayName = $"assets found: {VIEW_RESULTS.Count}" });

            int max = Mathf.Min(VIEW_RESULTS.Count, Mathf.Max(10, _resultsMaxRows));
            for (int i = 0; i < max; i++)
            {
                Result r = VIEW_RESULTS[i];
                table.children.Add(new DebugUI.Table.Row { displayName = r.ratio.ToString("F2") });
            }
        }

        private static void GenerateResultsColumns(DebugUI.Table table)
        {
            ASSET_COLUMN.AddHeader(table);
            ASSET_COLUMN.AddCells(table, VIEW_RESULTS);

            int row = -1;

            ((DebugUI.Table.Row)table.children[++row]).children.Add(new DebugUI.Value { displayName = "ScreenPx", getter = () => string.Empty });
            for (int i = 1; i < table.children.Count; i++)
            {
                int idx = i - 1;
                if (idx >= VIEW_RESULTS.Count)
                {
                    ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.Value { displayName = string.Empty, getter = () => string.Empty });
                    continue;
                }

                Result r = VIEW_RESULTS[idx];
                ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.Value { displayName = string.Empty, getter = () => r.screenPx.ToString("F0") });
            }

            row = -1;
            ((DebugUI.Table.Row)table.children[++row]).children.Add(new DebugUI.Value { displayName = "Texels", getter = () => string.Empty });
            for (int i = 1; i < table.children.Count; i++)
            {
                int idx = i - 1;
                if (idx >= VIEW_RESULTS.Count)
                {
                    ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.Value { displayName = string.Empty, getter = () => string.Empty });
                    continue;
                }

                Result r = VIEW_RESULTS[idx];
                ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.Value { displayName = string.Empty, getter = () => r.texels.ToString() });
            }

            row = -1;
            ((DebugUI.Table.Row)table.children[++row]).children.Add(new DebugUI.Value { displayName = "Severity", getter = () => string.Empty });
            for (int i = 1; i < table.children.Count; i++)
            {
                int idx = i - 1;
                if (idx >= VIEW_RESULTS.Count)
                {
                    ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.Value { displayName = string.Empty, getter = () => string.Empty });
                    continue;
                }

                Result r = VIEW_RESULTS[idx];
                Severity s = GetSeverity(r);
                Color col = SeverityToColor(s, r.ratio, _targetRatio);

                ((DebugUI.Table.Row)table.children[i]).children.Add(new DebugUI.ColorField
                {
                    displayName = idx.ToString(),
                    getter = () => col,
                    setter = _ => { },
                    showAlpha = false,
                    hdr = false
                });
            }
        }
        #endregion ResultsTable

        #region ScanFilterSort
        private static void ScanResults()
        {
            ALL_RESULTS.Clear();
            if (_checkType == ValidityCheckType.None)
                return;

            Camera cam = GetReferenceCamera();
            if (cam == null)
                return;

            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(cam);
            SCAN_MESHES.Clear();
            SCAN_SPRITES.Clear();
            FindAll(SCAN_MESHES);
            FindAll(SCAN_SPRITES);

            SEEN_RESULTS.Clear();

            if ((_rendererMask & RendererTypes.MeshRenderer) != 0)
            {
                for (int i = 0; i < SCAN_MESHES.Count && ALL_RESULTS.Count < MAX_SCAN_BUFFER; i++)
                {
                    MeshRenderer mr = SCAN_MESHES[i];
                    if (mr == null || !mr.enabled) continue;

                    Bounds b = mr.bounds;
                    if (!GeometryUtility.TestPlanesAABB(frustum, b)) continue;

                    Texture tex = TryGetMainTex(mr.sharedMaterial);
                    if (tex == null) continue;

                    if (!TryGetProjectedMaxScreenSize(cam, b, out float screenPx)) continue;

                    EntityId id = mr.gameObject.GetEntityId();
                    if (!SEEN_RESULTS.Add(id)) continue;

                    int texMax = Math.Max(tex.width, tex.height);
                    float ratio = texMax / screenPx;

                    ALL_RESULTS.Add(new Result { instanceId = id, name = mr.gameObject.name, ratio = ratio, screenPx = screenPx, texels = texMax });
                }
            }

            if ((_rendererMask & RendererTypes.SpriteRenderer) != 0 && ALL_RESULTS.Count < MAX_SCAN_BUFFER)
            {
                for (int i = 0; i < SCAN_SPRITES.Count && ALL_RESULTS.Count < MAX_SCAN_BUFFER; i++)
                {
                    SpriteRenderer sr = SCAN_SPRITES[i];
                    if (sr == null || !sr.enabled || sr.sprite == null) continue;

                    Bounds b = sr.bounds;
                    if (!GeometryUtility.TestPlanesAABB(frustum, b)) continue;

                    if (!TryGetProjectedMaxScreenSize(cam, b, out float screenPx)) continue;

                    EntityId id = sr.gameObject.GetEntityId();
                    if (!SEEN_RESULTS.Add(id)) continue;

                    int texMax = Mathf.Max((int)sr.sprite.rect.width, (int)sr.sprite.rect.height);
                    float ratio = texMax / screenPx;

                    ALL_RESULTS.Add(new Result { instanceId = id, name = sr.gameObject.name, ratio = ratio, screenPx = screenPx, texels = texMax });
                }
            }

            RefreshResultsView();
        }

        private static void RefreshResultsView()
        {
            VIEW_RESULTS.Clear();

            for (int i = 0; i < ALL_RESULTS.Count; i++)
            {
                Result r = ALL_RESULTS[i];
                if (ShouldListSeverity(GetSeverity(r)) && ShouldListDirection(GetDirection(r))) VIEW_RESULTS.Add(r);
            }

            VIEW_RESULTS.Sort(CompareResultsDynamic);
            if (!SORT_ASCENDING) VIEW_RESULTS.Reverse();
        }

        private static int CompareResultsDynamic(Result a, Result b)
        {
            switch (SORT_COLUMN)
            {
                case SortColumn.Asset:
                {
                    int n = string.Compare(a.name, b.name, StringComparison.Ordinal);
                    if (n != 0)
                        return n;
                    goto case SortColumn.Severity;
                }

                case SortColumn.Severity:
                {
                    int s = GetSeverity(a).CompareTo(GetSeverity(b));
                    if (s != 0)
                        return s;

                    int d = DirectionRank(a).CompareTo(DirectionRank(b));
                    if (d != 0)
                        return d;

                    float ea = ErrorFromTarget(a.ratio);
                    float eb = ErrorFromTarget(b.ratio);
                    int e = eb.CompareTo(ea);
                    if (e != 0)
                        return e;

                    int p = a.screenPx.CompareTo(b.screenPx);
                    if (p != 0)
                        return p;

                    int t = a.texels.CompareTo(b.texels);
                    if (t != 0)
                        return t;

                   
                    return string.Compare(a.name, b.name, StringComparison.Ordinal);
                }

                case SortColumn.Ratio:
                {
                    float ea = ErrorFromTarget(a.ratio);
                    float eb = ErrorFromTarget(b.ratio);
                    int e = ea.CompareTo(eb);
                    if (e != 0)
                        return e;
                    goto case SortColumn.ScreenPx;
                }

                case SortColumn.ScreenPx:
                {
                    int p = a.screenPx.CompareTo(b.screenPx);
                    if (p != 0)
                        return p;
                    goto case SortColumn.Texels;
                }

                case SortColumn.Texels:
                default:
                   
                    return a.texels.CompareTo(b.texels);
            }
        }
        #endregion ScanFilterSort

        #region Classification
        private static int DirectionRank(Result r)
        {
           
            return r.ratio < Mathf.Max(MIN_TARGET_RATIO, _targetRatio) ? 0 : 1;
        }

        private static TargetDirection GetDirection(Result r)
        {
           
            return r.ratio >= Mathf.Max(MIN_TARGET_RATIO, _targetRatio) ? TargetDirection.OverTarget : TargetDirection.UnderTarget;
        }

        private static bool ShouldListDirection(TargetDirection d)
        {
           
            return (d == TargetDirection.UnderTarget && (_targetDirectionMask & TargetDirectionMask.UnderTarget) != 0)
                   || (d == TargetDirection.OverTarget && (_targetDirectionMask & TargetDirectionMask.OverTarget) != 0);
        }

        private static Severity GetSeverity(Result r)
        {
           
            return ClassifySeverity(r.ratio, _targetRatio, _goodTolerance, _almostTolerance);
        }

        private static bool ShouldListSeverity(Severity s)
        {
           
            return (s == Severity.Bad && (_severityMask & SeverityMask.Bad) != 0)
                   || (s == Severity.Almost && (_severityMask & SeverityMask.Almost) != 0)
                   || (s == Severity.Good && (_severityMask & SeverityMask.Good) != 0);
        }

        private static float ErrorFromTarget(float ratio)
        {
            float rel = ratio / Mathf.Max(MIN_TARGET_RATIO, _targetRatio);
           
            return Mathf.Abs(rel - 1f);
        }

        private static Severity ClassifySeverity(float ratio, float target, float goodTol, float almostTol)
        {
            float rel = ratio / Mathf.Max(MIN_TARGET_RATIO, target);
            float err = Mathf.Abs(rel - 1f);
            if (err <= goodTol)
                return Severity.Good;
            if (err <= almostTol)
                return Severity.Almost;
           
            return Severity.Bad;
        }

        private static Color SeverityToColor(Severity s, float ratio, float target)
        {
            float rel = ratio / Mathf.Max(MIN_TARGET_RATIO, target);
            float err = Mathf.Abs(rel - 1f);
            const float cap = 1f;

            Color green = Color.green;
            Color yellow = new(1f, 0.85f, 0f);
            Color red = Color.red;
            Color cyan = Color.cyan;
            Color deepBlue = new(0.05f, 0.2f, 0.9f);

            TargetDirection d = ratio >= target ? TargetDirection.OverTarget : TargetDirection.UnderTarget;

            switch (s)
            {
                case Severity.Good:
                    return green;
                case Severity.Almost:
                {
                    float t = Mathf.InverseLerp(_goodTolerance, _almostTolerance, err);
                   
                    return d == TargetDirection.UnderTarget ? Color.Lerp(green, yellow, t) : Color.Lerp(green, cyan, t);
                }
                default:
                {
                    float t = Mathf.InverseLerp(_almostTolerance, cap, err);
                   
                    return d == TargetDirection.UnderTarget ? Color.Lerp(yellow, red, t) : Color.Lerp(cyan, deepBlue, t);
                }
            }
        }
        #endregion Classification

        #region Utilities
        private static bool TryGetProjectedMaxScreenSize(Camera camera, Bounds worldBounds, out float maxPx)
        {
            maxPx = 0f;
            if (camera == null || camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
                return false;

            FillBoundsCorners(worldBounds, BOUNDS_CORNERS);

            bool hasProjectedCorner = false;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < BOUNDS_CORNERS.Length; i++)
            {
                Vector3 screen = camera.WorldToScreenPoint(BOUNDS_CORNERS[i]);
                if (screen.z <= 0f)
                    continue;

                float x = Mathf.Clamp(screen.x, 0f, camera.pixelWidth);
                float y = Mathf.Clamp(screen.y, 0f, camera.pixelHeight);

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
                hasProjectedCorner = true;
            }

            if (!hasProjectedCorner)
                return false;

            maxPx = Mathf.Max(maxX - minX, maxY - minY);

            return maxPx > 0.0001f;
        }

        private static void FillBoundsCorners(Bounds bounds, Vector3[] corners)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(max.x, min.y, min.z);
            corners[2] = new Vector3(min.x, max.y, min.z);
            corners[3] = new Vector3(max.x, max.y, min.z);
            corners[4] = new Vector3(min.x, min.y, max.z);
            corners[5] = new Vector3(max.x, min.y, max.z);
            corners[6] = new Vector3(min.x, max.y, max.z);
            corners[7] = new Vector3(max.x, max.y, max.z);
        }

        private static Texture TryGetMainTex(Material m)
        {
            if (m == null || m.shader == null)
                return null;

            int count = m.shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if ((m.shader.GetPropertyFlags(i) & ShaderPropertyFlags.MainTexture) != 0)
                    return m.mainTexture;
            }

           
            return null;
        }

        private static void FindAll<T>(List<T> list) where T : Component
        {
            list.Clear();

            T[] arr = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            if (arr == null || arr.Length == 0)
                return;

            if (arr.Length > list.Capacity) list.Capacity = arr.Length;
            for (int i = 0; i < arr.Length; i++) list.Add(arr[i]);
        }

        private static bool TryComputeVisualInfo(string label, Bounds worldBounds, int sourceTexMaxDimPx, out Camera cam, out float screenPx, out float ratio, out Color color)
        {
            cam = GetVisualReferenceCamera();
            screenPx = 0f;
            ratio = 0f;
            color = default;

            if (cam == null || _checkType != ValidityCheckType.TexelDensity)
                return false;
            if (!TryGetProjectedMaxScreenSize(cam, worldBounds, out screenPx))
                return false;

            ratio = sourceTexMaxDimPx / screenPx;
            Severity sev = ClassifySeverity(ratio, _targetRatio, _goodTolerance, _almostTolerance);
            TargetDirection dir = ratio >= Mathf.Max(MIN_TARGET_RATIO, _targetRatio) ? TargetDirection.OverTarget : TargetDirection.UnderTarget;
            if (!ShouldListSeverity(sev) || !ShouldListDirection(dir))
                return false;

            color = SeverityToColor(sev, ratio, _targetRatio);
           
            return true;
        }
        #endregion Utilities
    }
}
