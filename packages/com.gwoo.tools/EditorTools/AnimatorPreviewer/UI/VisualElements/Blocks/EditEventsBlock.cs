using System;
using System.Collections.Generic;
using GWOO.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GWOO.Editor.Tools
{
	internal sealed class EditEventsBlock : VisualElement, IDisposable
	{
		private const float LIST_ROW_H = 22f;
		private const int LIST_MAX_VISIBLE_ROWS = 8;
		private const float LIST_EMPTY_H = 44f;

		private readonly IAnimatorPreviewerHost _host;
		private readonly ClipEventsEditModel _model = new();

		private bool _ignore;
		private bool _uiEnabled;
		private bool _disposed;
		private int _lastClipRevision = -1;

		public bool HasUnappliedChanges => _model.Dirty;

		/// <summary>True while time is being dragged (marker drag or time field drag).</summary>
		public event Action<bool> OnTimeDragStateChanged; 
		
		public event Action OnTimelineMarkersDirty;

		public int SelectedEventIndex => _model.SelectedEventIndex;

		// Header
		private Label _headerCount;
		private Label _headerDirty;

		private CustomButton _applyBtn;
		private CustomButton _revertBtn;

		private CustomButton _addBtn;
		private CustomButton _deleteBtn;

		// List
		private ListView _list;
		private int _lastRowCount = -1;

		// Details
		private TextField _fnField;
		private FloatField _timeField;
		private Label _frameLabel;

		private EnumField _paramKindField;
		private IntegerField _intField;
		private FloatField _floatField;
		private TextField _stringField;
		private ObjectField _objectField;

		// Freeze list resort while dragging time/markers so UX stays stable
		private int _timeDragDepth;
		private bool SuppressResort => _timeDragDepth > 0;

		// Guard time-field pointer lifecycle
		private int _timeFieldPointerId = -1;

		private readonly CallbackScope _callbacks = new();

		public EditEventsBlock(IAnimatorPreviewerHost host)
		{
			_host = host;

			style.flexDirection = FlexDirection.Column;

			BuildUI();

			EventCallback<DetachFromPanelEvent> detachCb = _ => ForceStopTimeDrag();
			RegisterCallback(detachCb);
			_callbacks.Add(() => UnregisterCallback(detachCb));
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_lastClipRevision = -1;

			ForceStopTimeDrag();

			_callbacks.Clear();
			OnTimeDragStateChanged = null;
			OnTimelineMarkersDirty = null;

			_headerCount = null;
			_headerDirty = null;

			_applyBtn = null;
			_revertBtn = null;
			_addBtn = null;
			_deleteBtn = null;

			_list = null;

			_fnField = null;
			_timeField = null;
			_frameLabel = null;

			_paramKindField = null;
			_intField = null;
			_floatField = null;
			_stringField = null;
			_objectField = null;
		}

		public AnimationEvent[] GetWorkingEventsForTimeline() => _model.workingEvents;

		public void BeginExternalTimeDrag() => PushTimeDrag();

		public void EndExternalTimeDrag()
		{
			bool wasSuppressed = SuppressResort;

			PopTimeDrag();

			if (wasSuppressed && !SuppressResort)
				ReorderListAndRestoreSelection();
		}

		public void SelectEvent(int eventIndex)
		{
			if (_model.workingEvents == null || _model.workingEvents.Length == 0) return;
			if (eventIndex < 0 || eventIndex >= _model.workingEvents.Length) return;

			_model.SelectedEventIndex = eventIndex;

			SelectRowForEventIndex(eventIndex);
			RefreshDetailsOnly();
			UpdateActionButtons();

			JumpPlayheadToEvent(_model.workingEvents[eventIndex]);
		}

		public void NotifyExternalEventTimeChange(int eventIndex, float newClipTimeSec)
		{
			if (_model.workingEvents == null) return;
			if (eventIndex < 0 || eventIndex >= _model.workingEvents.Length) return;

			int fps = SafeFps();
			float t = _model.SnapClipTime(newClipTimeSec, fps);

			_model.SetTime(eventIndex, t);

			_model.RecomputeDirty();
			UpdateDirtyUI();

			if (SuppressResort)
			{
				_list?.RefreshItems();
				RefreshDetailsOnly();
				return;
			}

			ReorderListAndRestoreSelection();
			RefreshDetailsOnly();
		}
		
		private void NotifyTimelineMarkersDirty()
		{
			OnTimelineMarkersDirty?.Invoke();
		}

		public void Apply()
		{
			if (!_uiEnabled) return;
			if (!_model.Dirty || _model.animationClip == null) return;

			bool hadSel = _model.HasValidSelection;
			EventSignature sig = _model.CaptureSelectionSignature();

			AnimationEvent[] toWrite = ClipEventsUtility.CloneEvents(_model.workingEvents);
			ClipEventsUtility.SortEventsStable(toWrite);

			bool ok = _host.TryApplyClipEvents(_model.animationClip, toWrite, "Apply Clip Events", out AnimationClip refreshed);
			if (!ok)
			{
				EditorUtility.DisplayDialog(
					"Apply Clip Events",
					"Failed to apply clip events.\n\nCheck the Console for details.",
					"OK");
				return;
			}

			// Model clips can get recreated on reimport.
			AnimationClip clipNow = refreshed != null ? refreshed : _host.TryRefreshClipReference(_model.animationClip);
			if (clipNow != null && clipNow != _model.animationClip)
			{
				_model.SetClip(clipNow);
				_host.CmdSetPreviewClip(clipNow);
			}

			_model.LoadFromAsset(_host.GetClipEventsSafe(_model.animationClip));
			_model.RestoreSelectionFromSignature(hadSel, sig);

			UpdateHeader();
			ReorderListAndRestoreSelection();
			RefreshDetailsOnly();
			UpdateDirtyUI();
			UpdateActionButtons();
			NotifyTimelineMarkersDirty();
		}

		public void Revert()
		{
			if (!_uiEnabled) return;
			if (_model.animationClip == null) return;

			bool hadSel = _model.HasValidSelection;
			EventSignature sig = _model.CaptureSelectionSignature();

			_model.LoadFromAsset(_host.GetClipEventsSafe(_model.animationClip));
			_model.RestoreSelectionFromSignature(hadSel, sig);

			UpdateHeader();
			ReorderListAndRestoreSelection();
			RefreshDetailsOnly();
			UpdateDirtyUI();
			UpdateActionButtons();
			NotifyTimelineMarkersDirty();
		}

		public void Refresh()
		{
			AnimatorPreviewerViewState s = _host.GetViewState();

			_uiEnabled = s.isBound && s.mode == AnimatorPreviewerMode.Clip && s.previewClip != null;
			SetEnabled(_uiEnabled);

			AnimationClip newClip = s.previewClip;
			if (_model.animationClip != newClip)
			{
				_model.SetClip(newClip);
				_model.LoadFromAsset(_host.GetClipEventsSafe(newClip));
				_lastClipRevision = s.clipEventsRevision;
			}
			else if (s.clipEventsRevision != _lastClipRevision && !HasUnappliedChanges)
			{
				_model.LoadFromAsset(_host.GetClipEventsSafe(newClip));
				_lastClipRevision = s.clipEventsRevision;
			}

			UpdateHeader();

			ReorderListAndRestoreSelection(rebuildEvenIfSuppressed: true);

			UpdateDirtyUI();
			UpdateActionButtons();
			RefreshDetailsOnly();
		}

		// ---------------- UI ----------------

		private void BuildUI()
		{
			Add(BuildHeaderRow());
			Add(BuildActionsRow());
			Add(new Separator(1));

			_list = new ListView(_model.rowToEventIndex, (int)LIST_ROW_H, MakeRow, BindRow)
			{
				selectionType = SelectionType.Single,
				virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
				style =
				{
					marginTop = 6,
					flexGrow = 0,
					flexShrink = 0
				}
			};

			_list.selectionChanged += OnSelectionChanged;
			_callbacks.Add(() =>
			{
				if (_list != null)
					_list.selectionChanged -= OnSelectionChanged;
			});

			Add(_list);

			Add(BuildDetails());

			UpdateDirtyUI();
			UpdateListHeight();
			UpdateActionButtons();
		}

		private VisualElement BuildHeaderRow()
		{
			VisualElement header = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexWrap = Wrap.Wrap,
					marginBottom = 4
				}
			};

			Label title = new("Count:")
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					unityFontStyleAndWeight = FontStyle.Bold,
					marginRight = 4
				}
			};
			header.Add(title);

			_headerCount = new Label
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					unityFontStyleAndWeight = FontStyle.Bold,
					marginRight = 8
				}
			};
			header.Add(_headerCount);

			_headerDirty = new Label("• Unapplied changes")
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					display = DisplayStyle.None,
					opacity = 0.5f,
					marginRight = 10
				}
			};
			header.Add(_headerDirty);

			header.Add(new VisualElement { style = { flexGrow = 1f } });

			_applyBtn = new CustomButton(Apply)
			{
				text = "Apply",
				Width = 0,
				tooltip = "Write staged events to the clip asset/importer.",
				style =
				{
					minWidth = 70,
					marginRight = 6
				}
			};
			_applyBtn.AddToClassList("primary-color");
			header.Add(_applyBtn);

			_revertBtn = new CustomButton(Revert)
			{
				text = "Revert",
				Width = 0,
				tooltip = "Discard staged edits and reload events from the asset/importer.",
				style =
				{
					minWidth = 74
				}
			};
			_revertBtn.AddToClassList("secondary-color");
			header.Add(_revertBtn);

			return header;
		}

		private VisualElement BuildActionsRow()
		{
			VisualElement actionsRow = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexWrap = Wrap.Wrap,
					marginTop = 6,
					marginBottom = 2
				}
			};

			actionsRow.Add(new VisualElement { style = { flexGrow = 1f } });

			_addBtn = new CustomButton(AddEvent)
			{
				text = "+ Add",
				Width = 0,
				tooltip = "Add a new event at current playhead time (staged).",
				style =
				{
					minWidth = 70,
					marginRight = 6
				}
			};
			actionsRow.Add(_addBtn);

			_deleteBtn = new CustomButton(DeleteSelected)
			{
				text = "Delete",
				Width = 0,
				tooltip = "Delete selected event (staged).",
				style =
				{
					minWidth = 70
				}
			};
			actionsRow.Add(_deleteBtn);

			return actionsRow;
		}

		private VisualElement BuildDetails()
		{
			VisualElement details = new()
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					marginTop = 8,
					paddingLeft = 10,
					paddingRight = 10,
					paddingTop = 8,
					paddingBottom = 10,
					borderTopLeftRadius = 8,
					borderTopRightRadius = 8,
					borderBottomLeftRadius = 8,
					borderBottomRightRadius = 8,
					backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.06f))
				}
			};

			_fnField = new TextField("Function");
			EventCallback<ChangeEvent<string>> fnCb = evt =>
			{
				if (_ignore || !_model.HasValidSelection)
					return;

				_model.SetFunctionName(_model.SelectedEventIndex, evt.newValue);
				MarkDirtyAndRefreshUI(resort: true);
			};
			_fnField.RegisterValueChangedCallback(fnCb);
			_callbacks.Add(() => _fnField.UnregisterValueChangedCallback(fnCb));
			details.Add(_fnField);

			_timeField = new FloatField("Time (clip s)");
			InstallTimeDragGuards(_timeField);

			EventCallback<ChangeEvent<float>> timeCb = evt =>
			{
				if (_ignore || !_model.HasValidSelection)
					return;

				int fps = SafeFps();
				float t = _model.SnapClipTime(evt.newValue, fps);

				_model.SetTime(_model.SelectedEventIndex, t);

				MarkDirtyAndRefreshUI(resort: true);

				if (_model.HasValidSelection && _model.workingEvents != null)
					JumpPlayheadToEvent(_model.workingEvents[_model.SelectedEventIndex]);
			};
			_timeField.RegisterValueChangedCallback(timeCb);
			_callbacks.Add(() => _timeField.UnregisterValueChangedCallback(timeCb));
			details.Add(_timeField);

			_frameLabel = new Label
			{
				pickingMode = PickingMode.Ignore,
				style = { opacity = 0.75f, fontSize = 11, marginBottom = 6 }
			};
			details.Add(_frameLabel);

			_paramKindField = new EnumField("Param", ClipEventParamType.None);
			EventCallback<ChangeEvent<Enum>> kindCb = evt =>
			{
				if (_ignore || !_model.HasValidSelection)
					return;

				ClipEventParamType k = (ClipEventParamType)evt.newValue;
				_model.SetParamKind(_model.SelectedEventIndex, k);

				MarkDirtyAndRefreshUI(resort: false);
				RefreshDetailsOnly();
			};
			_paramKindField.RegisterValueChangedCallback(kindCb);
			_callbacks.Add(() => _paramKindField.UnregisterValueChangedCallback(kindCb));
			details.Add(_paramKindField);

			_intField = new IntegerField("Int") { style = { display = DisplayStyle.None } };
			EventCallback<ChangeEvent<int>> intCb = evt =>
			{
				if (_ignore || !_model.HasValidSelection)
					return;

				_model.SetInt(_model.SelectedEventIndex, evt.newValue);
				MarkDirtyAndRefreshUI(resort: false);
			};
			_intField.RegisterValueChangedCallback(intCb);
			_callbacks.Add(() => _intField.UnregisterValueChangedCallback(intCb));
			details.Add(_intField);

			_floatField = new FloatField("Float") { style = { display = DisplayStyle.None } };
			EventCallback<ChangeEvent<float>> floatCb = evt =>
			{
				if (_ignore || !_model.HasValidSelection)
					return;

				_model.SetFloat(_model.SelectedEventIndex, evt.newValue);
				MarkDirtyAndRefreshUI(resort: false);
			};
			_floatField.RegisterValueChangedCallback(floatCb);
			_callbacks.Add(() => _floatField.UnregisterValueChangedCallback(floatCb));
			details.Add(_floatField);

			_stringField = new TextField("String") { style = { display = DisplayStyle.None } };
			EventCallback<ChangeEvent<string>> stringCb = evt =>
			{
				if (_ignore || !_model.HasValidSelection)
					return;

				_model.SetString(_model.SelectedEventIndex, evt.newValue);
				MarkDirtyAndRefreshUI(resort: false);
			};
			_stringField.RegisterValueChangedCallback(stringCb);
			_callbacks.Add(() => _stringField.UnregisterValueChangedCallback(stringCb));
			details.Add(_stringField);

			_objectField = new ObjectField("Object")
			{
				allowSceneObjects = false,
				style = { display = DisplayStyle.None }
			};

			EventCallback<ChangeEvent<Object>> objCb = evt =>
			{
				if (_ignore || !_model.HasValidSelection)
					return;

				Object obj = evt.newValue;
				if (obj != null && !EditorUtility.IsPersistent(obj))
					obj = null;

				_model.SetObject(_model.SelectedEventIndex, obj);
				MarkDirtyAndRefreshUI(resort: false);
			};

			_objectField.RegisterValueChangedCallback(objCb);
			_callbacks.Add(() => _objectField.UnregisterValueChangedCallback(objCb));
			details.Add(_objectField);

			return details;
		}

		private void UpdateHeader()
		{
			int count = _model.workingEvents != null ? _model.workingEvents.Length : 0;
			_headerCount.text = count <= 0 ? "" : $"{count}";
		}

		// ---------------- Selection / list ----------------

		private VisualElement MakeRow()
		{
			VisualElement row = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					paddingLeft = 6,
					paddingRight = 6
				}
			};

			Label left = new()
			{
				name = "time",
				pickingMode = PickingMode.Ignore,
				style = { minWidth = 90, opacity = 0.85f }
			};

			Label mid = new()
			{
				name = "fn",
				pickingMode = PickingMode.Ignore,
				style = { flexGrow = 1f }
			};

			row.Add(left);
			row.Add(mid);
			return row;
		}

		private void BindRow(VisualElement element, int rowIndex)
		{
			if (rowIndex < 0 || rowIndex >= _model.rowToEventIndex.Count) return;
			if (_model.workingEvents == null || _model.workingEvents.Length == 0) return;

			int eventIndex = _model.rowToEventIndex[rowIndex];
			if (eventIndex < 0 || eventIndex >= _model.workingEvents.Length) return;

			int fps = SafeFps();
			AnimationEvent e = _model.workingEvents[eventIndex];
			if (e == null) return;

			Label time = element.Q<Label>("time");
			Label fn = element.Q<Label>("fn");

			int frame = Mathf.RoundToInt(e.time * fps);
			time.text = $"{e.time:0.###}s (f{frame})";

			string f = e.functionName;
			fn.text = string.IsNullOrEmpty(f) ? "(no function)" : f;
		}

		private void OnSelectionChanged(IEnumerable<object> selection)
		{
			if (_ignore) return;

			int picked = -1;
			foreach (object o in selection)
			{
				if (o is int eventIndex)
				{
					picked = eventIndex;
					break;
				}
			}

			_model.SelectedEventIndex = picked;

			RefreshDetailsOnly();
			UpdateActionButtons();

			if (_model.HasValidSelection && _model.workingEvents != null)
				JumpPlayheadToEvent(_model.workingEvents[_model.SelectedEventIndex]);
		}

		private void ReorderListAndRestoreSelection(bool rebuildEvenIfSuppressed = false)
		{
			if (SuppressResort && !rebuildEvenIfSuppressed)
			{
				_list?.RefreshItems();
				return;
			}

			_model.BuildSortedRowMap();
			UpdateListHeight();

			int rowCount = _model.rowToEventIndex.Count;
			bool countChanged = (_lastRowCount != rowCount);
			_lastRowCount = rowCount;

			_ignore = true;
			if (countChanged) _list?.Rebuild();
			else _list?.RefreshItems();
			_ignore = false;

			if (_model.HasValidSelection)
				SelectRowForEventIndex(_model.SelectedEventIndex);
			else
				ClearListSelection();

			UpdateActionButtons();
		}

		private void SelectRowForEventIndex(int eventIndex)
		{
			if (_model.workingEvents == null) return;
			if (eventIndex < 0 || eventIndex >= _model.workingEvents.Length)
				return;

			int row = _model.rowToEventIndex.IndexOf(eventIndex);
			if (row < 0)
				return;

			_ignore = true;
			_list.SetSelectionWithoutNotify(new[] { row });
			_ignore = false;
		}

		private void ClearListSelection()
		{
			_ignore = true;
			_list?.ClearSelection();
			_ignore = false;
		}

		private void UpdateListHeight()
		{
			if (_list == null)
				return;

			int count = _model.rowToEventIndex.Count;
			int rows = Mathf.Clamp(count, 0, LIST_MAX_VISIBLE_ROWS);

			float h = (rows <= 0) ? LIST_EMPTY_H : (rows * LIST_ROW_H) + 2f;
			_list.style.height = h;
		}

		// ---------------- Details ----------------

		private void RefreshDetailsOnly()
		{
			bool has = _model.HasValidSelection;

			_fnField.SetEnabled(_uiEnabled && has);
			_timeField.SetEnabled(_uiEnabled && has);
			_paramKindField.SetEnabled(_uiEnabled && has);

			_intField.SetEnabled(_uiEnabled && has);
			_floatField.SetEnabled(_uiEnabled && has);
			_stringField.SetEnabled(_uiEnabled && has);
			_objectField.SetEnabled(_uiEnabled && has);

			if (!has || _model.workingEvents == null)
			{
				SetParamUI(ClipEventParamType.None);

				_ignore = true;
				_fnField.SetValueWithoutNotify(string.Empty);
				_timeField.SetValueWithoutNotify(0f);
				_frameLabel.text = "";
				_paramKindField.SetValueWithoutNotify(ClipEventParamType.None);
				_intField.SetValueWithoutNotify(0);
				_floatField.SetValueWithoutNotify(0f);
				_stringField.SetValueWithoutNotify(string.Empty);
				_objectField.SetValueWithoutNotify(null);
				_ignore = false;

				return;
			}

			int fps = SafeFps();
			AnimationEvent e = _model.workingEvents[_model.SelectedEventIndex];
			if (e == null)
				return;

			ClipEventParamType type = _model.GetParamKind(e);

			_ignore = true;

			_fnField.SetValueWithoutNotify(e.functionName ?? string.Empty);
			_timeField.SetValueWithoutNotify(e.time);

			int frame = Mathf.RoundToInt(e.time * fps);
			_frameLabel.text = $"Frame (FPS={fps}): {frame}";

			_paramKindField.SetValueWithoutNotify(type);

			_intField.SetValueWithoutNotify(e.intParameter);
			_floatField.SetValueWithoutNotify(e.floatParameter);
			_stringField.SetValueWithoutNotify(e.stringParameter ?? string.Empty);
			_objectField.SetValueWithoutNotify(e.objectReferenceParameter);

			_ignore = false;

			SetParamUI(type);
			UpdateActionButtons();
		}

		private void SetParamUI(ClipEventParamType k)
		{
			_intField.style.display = (k == ClipEventParamType.Int) ? DisplayStyle.Flex : DisplayStyle.None;
			_floatField.style.display = (k == ClipEventParamType.Float) ? DisplayStyle.Flex : DisplayStyle.None;
			_stringField.style.display = (k == ClipEventParamType.String) ? DisplayStyle.Flex : DisplayStyle.None;
			_objectField.style.display = (k == ClipEventParamType.Object) ? DisplayStyle.Flex : DisplayStyle.None;
		}

		// ---------------- Dirty / actions ----------------

		private void UpdateDirtyUI()
		{
			_headerDirty.style.display = _model.Dirty ? DisplayStyle.Flex : DisplayStyle.None;

			bool canApply = _uiEnabled && _model.Dirty;

			_applyBtn?.SetEnabled(canApply);
			_revertBtn?.SetEnabled(canApply);
		}

		private void UpdateActionButtons()
		{
			bool canEdit = _uiEnabled && _model.animationClip != null;

			_addBtn?.SetEnabled(canEdit);
			_deleteBtn?.SetEnabled(canEdit && _model.HasValidSelection);
		}

		private void MarkDirtyAndRefreshUI(bool resort)
		{
			_model.RecomputeDirty();
			UpdateDirtyUI();

			if (!resort || SuppressResort)
				_list?.RefreshItems();
			else
				ReorderListAndRestoreSelection();

			UpdateActionButtons();
			NotifyTimelineMarkersDirty();
		}

		// ---------------- Time drag stability ----------------

		private void InstallTimeDragGuards(FloatField field)
		{
			if (field == null) return;

			VisualElement dragger = field.Q<VisualElement>("unity-dragger");
			VisualElement textInput = field.Q<VisualElement>("unity-text-input");

			RegisterTimeDragGuards(field);
			RegisterTimeDragGuards(dragger);
			RegisterTimeDragGuards(textInput);
		}

		private void RegisterTimeDragGuards(VisualElement ve)
		{
			if (ve == null) return;

			ve.RegisterCallback<PointerDownEvent>(OnTimeFieldPointerDown, TrickleDown.TrickleDown);
			ve.RegisterCallback<PointerUpEvent>(OnTimeFieldPointerUp, TrickleDown.TrickleDown);
			ve.RegisterCallback<PointerCancelEvent>(OnTimeFieldPointerCancel, TrickleDown.TrickleDown);
			ve.RegisterCallback<PointerCaptureOutEvent>(OnTimeFieldPointerCaptureOut, TrickleDown.TrickleDown);
			ve.RegisterCallback<FocusOutEvent>(OnTimeFieldFocusOut, TrickleDown.TrickleDown);

			_callbacks.Add(() => ve.UnregisterCallback<PointerDownEvent>(OnTimeFieldPointerDown, TrickleDown.TrickleDown));
			_callbacks.Add(() => ve.UnregisterCallback<PointerUpEvent>(OnTimeFieldPointerUp, TrickleDown.TrickleDown));
			_callbacks.Add(() => ve.UnregisterCallback<PointerCancelEvent>(OnTimeFieldPointerCancel, TrickleDown.TrickleDown));
			_callbacks.Add(() => ve.UnregisterCallback<PointerCaptureOutEvent>(OnTimeFieldPointerCaptureOut, TrickleDown.TrickleDown));
			_callbacks.Add(() => ve.UnregisterCallback<FocusOutEvent>(OnTimeFieldFocusOut, TrickleDown.TrickleDown));
		}

		private void OnTimeFieldFocusOut(FocusOutEvent _) => DisarmTimeFieldDrag();

		private void OnTimeFieldPointerDown(PointerDownEvent e)
		{
			if (e.button != 0) return;
			if (_timeFieldPointerId != -1) return;

			_timeFieldPointerId = e.pointerId;
			PushTimeDrag();
		}

		private void OnTimeFieldPointerUp(PointerUpEvent e)
		{
			if (_timeFieldPointerId == -1) return;
			if (e.pointerId != _timeFieldPointerId) return;

			DisarmTimeFieldDrag();
		}

		private void OnTimeFieldPointerCancel(PointerCancelEvent e)
		{
			if (_timeFieldPointerId == -1) return;
			if (e.pointerId != _timeFieldPointerId) return;

			DisarmTimeFieldDrag();
		}

		private void OnTimeFieldPointerCaptureOut(PointerCaptureOutEvent e)
		{
			if (_timeFieldPointerId == -1) return;
			DisarmTimeFieldDrag();
		}

		private void DisarmTimeFieldDrag()
		{
			if (_timeFieldPointerId == -1)
				return;

			_timeFieldPointerId = -1;

			bool wasSuppressed = SuppressResort;
			PopTimeDrag();

			if (wasSuppressed && !SuppressResort)
				ReorderListAndRestoreSelection();
		}

		private void ForceStopTimeDrag()
		{
			_timeFieldPointerId = -1;

			if (_timeDragDepth <= 0)
				return;

			_timeDragDepth = 0;
			OnTimeDragStateChanged?.Invoke(false);
		}

		private void PushTimeDrag()
		{
			bool wasSuppressed = SuppressResort;

			_timeDragDepth++;

			if (!wasSuppressed && SuppressResort)
				OnTimeDragStateChanged?.Invoke(true);
		}

		private void PopTimeDrag()
		{
			bool wasSuppressed = SuppressResort;

			_timeDragDepth = Mathf.Max(0, _timeDragDepth - 1);

			if (wasSuppressed && !SuppressResort)
				OnTimeDragStateChanged?.Invoke(false);
		}

		// ---------------- Commands ----------------

		private void AddEvent()
		{
			if (!_uiEnabled) return;
			if (_model.animationClip == null) return;

			AnimatorPreviewerViewState s = _host.GetViewState();

			float tlLen = Mathf.Max(1e-6f, s.timelineLength);
			float clipLen = Mathf.Max(1e-6f, _model.animationClip.length);

			float tlT = Mathf.Clamp((float)s.timelineTime, 0f, tlLen);
			float clipT = (tlT / tlLen) * clipLen;

			int fps = SafeFps();
			clipT = _model.SnapClipTime(clipT, fps);

			int newIdx = _model.AddEvent(clipT, defaultFn: "OnAnimEvent");
			_model.SelectedEventIndex = newIdx;

			_model.RecomputeDirty();
			UpdateHeader();
			UpdateDirtyUI();

			ReorderListAndRestoreSelection();
			RefreshDetailsOnly();
			UpdateActionButtons();
			NotifyTimelineMarkersDirty();

			if (_model.workingEvents != null && newIdx >= 0 && newIdx < _model.workingEvents.Length)
				JumpPlayheadToEvent(_model.workingEvents[newIdx]);
		}

		private void DeleteSelected()
		{
			if (!_uiEnabled) return;
			if (!_model.HasValidSelection) return;

			_model.DeleteEvent(_model.SelectedEventIndex);

			_model.RecomputeDirty();
			UpdateHeader();
			UpdateDirtyUI();

			ReorderListAndRestoreSelection();
			RefreshDetailsOnly();
			UpdateActionButtons();
			NotifyTimelineMarkersDirty();
		}

		private void JumpPlayheadToEvent(AnimationEvent e)
		{
			if (e == null) return;
			if (_model.animationClip == null) return;

			AnimatorPreviewerViewState s = _host.GetViewState();

			float tlLen = Mathf.Max(1e-6f, s.timelineLength);
			float clipLen = Mathf.Max(1e-6f, _model.animationClip.length);

			float tlT = Mathf.Clamp((e.time / clipLen) * tlLen, 0f, tlLen);

			_host.CmdTimelineScrubStart(tlT);
			_host.CmdTimelineScrubMove(tlT);
			_host.CmdTimelineScrubEnd(tlT);
		}

		private int SafeFps()
		{
			int fps = 60;
			try { fps = _host != null ? _host.GetViewState().fps : 60; }
			catch { /* ignored */ }

			return Mathf.Max(1, fps);
		}
	}
}

