using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class TimelineBarElement : VisualElement, IDisposable
	{
		private const float MIN_TIMELINE_LEN = 0.0001f;
		private const float MIN_CLIP_LEN = 1e-6f;

		private const float EVENT_HIT_X_PX = 7f;
		private const float EVENT_HIT_Y_PAD_PX = 5f;
		private const float EVENT_MARKER_TOP_PX = 6f;
		private const float EVENT_MARKER_BOTTOM_PX = 22f;

		private const string DEFAULT_TOOLTIP =
			"LMB drag: Scrub\nRMB drag: Set loop range\nRMB click: Clear loop range\nHover markers: Event info";

		// --- public API ---
		public float timelineLengthSec = 2f;
		public float playheadTimeSec;
		public int fps = 60;

		public bool drawEventMarkers;
		public AnimationEvent[] clipEvents;
		public float clipLengthSec = 1f;

		public bool hasLoopRange;
		public float loopRangeStartSec;
		public float loopRangeEndSec;

		public bool drawPlayhead = true;

		// Event editing hooks (pure staging)
		public bool editEventsMode;
		public int selectedEventIndex = -1;

		// Colors set by the caller.
		public Color backgroundColor = new(0.13f, 0.135f, 0.14f, 1f);
		public Color borderColor = new(0f, 0f, 0f, 0.30f);
		public Color tickColor = new(0.36f, 0.36f, 0.36f, 1f);
		public Color playheadColor = new(0.20f, 1.0f, 0.75f, 1f);

		public Color rangeAccent = new(0.16f, 0.62f, 0.42f, 1f);
		public Color eventMarkerColor = new(1.0f, 0.78f, 0.25f, 0.95f);
		public Color eventMarkerHoverColor = new(1.0f, 0.92f, 0.35f, 1f);

		// Events:
		public event Action<float> ScrubStarted;
		public event Action<float> Scrubbed;
		public event Action<float> ScrubEnded;

		public event Action<float, float> LoopRangeChanged; // start,end (sorted)
		public event Action LoopRangeCleared;

		public event Action<int> EventSelected;
		public event Action<int, float> EventDragStarted; // clip seconds
		public event Action<int, float> EventDragged;     // clip seconds
		public event Action<int, float> EventDragEnded;   // clip seconds

		private readonly CallbackScope _callbacks = new();

		// --- interaction state ---
		private bool _scrubbing;
		private int _scrubPointerId = -1;

		private bool _rangeSelecting;
		private int _rangePointerId = -1;
		private float _rangeA;
		private float _rangeB;
		private float _rangeDownX;
		private bool _rangeMoved;

		private bool _draggingEvent;
		private int _eventPointerId = -1;
		private int _dragEventIndex = -1;

		private int _hoveredEventIndex = -1;

		private bool _disposed;

		public TimelineBarElement()
		{
			name = "TimelineBar";
			pickingMode = PickingMode.Position;
			tooltip = DEFAULT_TOOLTIP;

			generateVisualContent += OnGenerateVisualContent;
			_callbacks.Add(() => generateVisualContent -= OnGenerateVisualContent);

			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
			RegisterCallback<PointerCancelEvent>(OnPointerCancel);

			_callbacks.Add(() => UnregisterCallback<PointerDownEvent>(OnPointerDown));
			_callbacks.Add(() => UnregisterCallback<PointerMoveEvent>(OnPointerMove));
			_callbacks.Add(() => UnregisterCallback<PointerUpEvent>(OnPointerUp));
			_callbacks.Add(() => UnregisterCallback<PointerCancelEvent>(OnPointerCancel));

			RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
			RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
			RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

			_callbacks.Add(() => UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut));
			_callbacks.Add(() => UnregisterCallback<PointerLeaveEvent>(OnPointerLeave));
			_callbacks.Add(() => UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel));
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			ForceReleaseAllPointers(sendEndEvents: true);
			_callbacks.Clear();

			ScrubStarted = null;
			Scrubbed = null;
			ScrubEnded = null;

			LoopRangeChanged = null;
			LoopRangeCleared = null;

			EventSelected = null;
			EventDragStarted = null;
			EventDragged = null;
			EventDragEnded = null;
		}

		private void OnPointerCaptureOut(PointerCaptureOutEvent _) => ForceReleaseAllPointers(sendEndEvents: true);
		private void OnPointerLeave(PointerLeaveEvent _) => ClearHover();
		private void OnDetachFromPanel(DetachFromPanelEvent _) => ForceReleaseAllPointers(sendEndEvents: true);

		private void OnGenerateVisualContent(MeshGenerationContext mgc)
		{
			Rect r = contentRect;
			if (r.width <= 1f || r.height <= 1f)
				return;

			// Clamp hovered/selected indices when clipEvents changes.
			if (clipEvents == null || clipEvents.Length == 0)
			{
				_hoveredEventIndex = -1;
				if (selectedEventIndex >= 0) selectedEventIndex = -1;
			}
			else
			{
				if (_hoveredEventIndex >= clipEvents.Length) _hoveredEventIndex = -1;
				if (selectedEventIndex >= clipEvents.Length) selectedEventIndex = -1;
			}

			Painter2D p = mgc.painter2D;

			p.fillColor = backgroundColor;
			p.BeginPath();
			p.MoveTo(new Vector2(r.xMin, r.yMin));
			p.LineTo(new Vector2(r.xMax, r.yMin));
			p.LineTo(new Vector2(r.xMax, r.yMax));
			p.LineTo(new Vector2(r.xMin, r.yMax));
			p.ClosePath();
			p.Fill();

			float len = Mathf.Max(MIN_TIMELINE_LEN, timelineLengthSec);

			// Loop range overlay
			if (hasLoopRange)
			{
				float a = Mathf.Clamp(loopRangeStartSec, 0f, timelineLengthSec);
				float b = Mathf.Clamp(loopRangeEndSec, 0f, timelineLengthSec);
				if (b < a) (a, b) = (b, a);

				float xA = Mathf.Lerp(r.xMin, r.xMax, a / len);
				float xB = Mathf.Lerp(r.xMin, r.xMax, b / len);

				p.fillColor = new Color(rangeAccent.r, rangeAccent.g, rangeAccent.b, 0.10f);
				p.BeginPath();
				p.MoveTo(new Vector2(xA, r.yMin));
				p.LineTo(new Vector2(xB, r.yMin));
				p.LineTo(new Vector2(xB, r.yMax));
				p.LineTo(new Vector2(xA, r.yMax));
				p.ClosePath();
				p.Fill();

				p.strokeColor = new Color(rangeAccent.r, rangeAccent.g, rangeAccent.b, 0.55f);
				p.lineWidth = 1.5f;

				p.BeginPath();
				p.MoveTo(new Vector2(xA, r.yMin));
				p.LineTo(new Vector2(xA, r.yMax));
				p.Stroke();

				p.BeginPath();
				p.MoveTo(new Vector2(xB, r.yMin));
				p.LineTo(new Vector2(xB, r.yMax));
				p.Stroke();
			}

			// Top/bottom border strokes
			p.strokeColor = borderColor;
			p.lineWidth = 1f;

			p.BeginPath();
			p.MoveTo(new Vector2(r.xMin, r.yMin + 0.5f));
			p.LineTo(new Vector2(r.xMax, r.yMin + 0.5f));
			p.Stroke();

			p.BeginPath();
			p.MoveTo(new Vector2(r.xMin, r.yMax - 0.5f));
			p.LineTo(new Vector2(r.xMax, r.yMax - 0.5f));
			p.Stroke();

			// Frame ticks (bottom ticks, seconds taller)
			int playbackFps = Mathf.Max(1, fps);
			int totalFrames = Mathf.Max(1, Mathf.RoundToInt(len * playbackFps));

			p.strokeColor = tickColor;
			p.lineWidth = 1f;

			for (int f = 0; f <= totalFrames; f++)
			{
				float tSec = f / (float)playbackFps;
				float x = Mathf.Lerp(r.xMin, r.xMax, tSec / len);

				bool sec = (f % playbackFps) == 0;
				float h = sec ? 18f : 8f;

				p.BeginPath();
				p.MoveTo(new Vector2(x, r.yMax));
				p.LineTo(new Vector2(x, r.yMax - h));
				p.Stroke();
			}

			// Event markers
			bool canMarkers = drawEventMarkers
			                  && clipEvents != null
			                  && clipEvents.Length > 0
			                  && clipLengthSec > MIN_CLIP_LEN;

			if (canMarkers)
			{
				float clipLen = Mathf.Max(MIN_CLIP_LEN, clipLengthSec);

				for (int i = 0; i < clipEvents.Length; i++)
				{
					AnimationEvent ev = clipEvents[i];
					if (ev == null)
						continue;

					bool hovered = (i == _hoveredEventIndex);
					bool selected = (i == selectedEventIndex);

					float width = selected ? 3.5f : (hovered ? 3f : 2f);

					p.strokeColor = hovered ? eventMarkerHoverColor : eventMarkerColor;
					p.lineWidth = width;

					float evClipT = Mathf.Clamp(ev.time, 0f, clipLen);
					float x01 = evClipT / clipLen;
					float x = Mathf.Lerp(r.xMin, r.xMax, x01);

					p.BeginPath();
					p.MoveTo(new Vector2(x, r.yMin + EVENT_MARKER_TOP_PX));
					p.LineTo(new Vector2(x, r.yMin + EVENT_MARKER_BOTTOM_PX));
					p.Stroke();
				}
			}

			if (!drawPlayhead || _draggingEvent)
				return;

			float wrapped = Mathf.Clamp(playheadTimeSec, 0f, len);
			float px = Mathf.Lerp(r.xMin, r.xMax, wrapped / len);

			p.strokeColor = playheadColor;
			p.lineWidth = 2.25f;

			p.BeginPath();
			p.MoveTo(new Vector2(px, r.yMin));
			p.LineTo(new Vector2(px, r.yMax));
			p.Stroke();
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (evt == null)
				return;

			switch (evt.button)
			{
				case (int)MouseButton.LeftMouse:
				{
					if (editEventsMode && TryPickEventIndex(evt.localPosition, out int picked))
					{
						selectedEventIndex = picked;
						EventSelected?.Invoke(picked);

						_draggingEvent = true;
						_eventPointerId = evt.pointerId;
						_dragEventIndex = picked;

						PointerCaptureHelper.CapturePointer(this, _eventPointerId);

						float clipT = LocalXToClipTime(evt.localPosition.x);
						playheadTimeSec = ClipTimeToTimelineTime(clipT);

						EventDragStarted?.Invoke(picked, clipT);

						MarkDirtyRepaint();
						evt.StopPropagation();
						return;
					}

					_scrubbing = true;
					_scrubPointerId = evt.pointerId;

					PointerCaptureHelper.CapturePointer(this, _scrubPointerId);

					float t = LocalXToTimeTimeline(evt.localPosition.x);
					playheadTimeSec = t;

					ScrubStarted?.Invoke(t);

					MarkDirtyRepaint();
					evt.StopPropagation();
					return;
				}
				case (int)MouseButton.RightMouse:
				{
					_rangeSelecting = true;
					_rangePointerId = evt.pointerId;
					_rangeDownX = evt.localPosition.x;
					_rangeMoved = false;

					float t = LocalXToTimeTimeline(evt.localPosition.x);
					_rangeA = t;
					_rangeB = t;

					PointerCaptureHelper.CapturePointer(this, _rangePointerId);

					hasLoopRange = true;
					loopRangeStartSec = t;
					loopRangeEndSec = t;

					LoopRangeChanged?.Invoke(t, t);

					MarkDirtyRepaint();
					evt.StopPropagation();
					break;
				}
			}
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (evt == null)
				return;

			if (_draggingEvent)
			{
				if (evt.pointerId != _eventPointerId)
					return;

				if (!PointerCaptureHelper.HasPointerCapture(this, _eventPointerId))
					return;

				float clipT = LocalXToClipTime(evt.localPosition.x);
				playheadTimeSec = ClipTimeToTimelineTime(clipT);

				EventDragged?.Invoke(_dragEventIndex, clipT);

				MarkDirtyRepaint();
				evt.StopPropagation();
				return;
			}

			if (_scrubbing)
			{
				if (evt.pointerId != _scrubPointerId)
					return;

				if (!PointerCaptureHelper.HasPointerCapture(this, _scrubPointerId))
					return;

				float t = LocalXToTimeTimeline(evt.localPosition.x);
				playheadTimeSec = t;

				Scrubbed?.Invoke(t);

				MarkDirtyRepaint();
				evt.StopPropagation();
				return;
			}

			if (_rangeSelecting)
			{
				if (evt.pointerId != _rangePointerId)
					return;

				if (!PointerCaptureHelper.HasPointerCapture(this, _rangePointerId))
					return;

				_rangeMoved |= Mathf.Abs(evt.localPosition.x - _rangeDownX) > 3f;

				float t = LocalXToTimeTimeline(evt.localPosition.x);
				_rangeB = t;

				float start = Mathf.Min(_rangeA, _rangeB);
				float end = Mathf.Max(_rangeA, _rangeB);

				hasLoopRange = true;
				loopRangeStartSec = start;
				loopRangeEndSec = end;

				LoopRangeChanged?.Invoke(start, end);

				MarkDirtyRepaint();
				evt.StopPropagation();
				return;
			}

			UpdateHover(evt.localPosition);
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (evt == null)
				return;

			if (_draggingEvent)
			{
				if (evt.pointerId != _eventPointerId)
					return;

				float clipT = LocalXToClipTime(evt.localPosition.x);
				playheadTimeSec = ClipTimeToTimelineTime(clipT);

				EventDragEnded?.Invoke(_dragEventIndex, clipT);

				ForceReleaseEventPointer(sendEndEvent: false);

				MarkDirtyRepaint();
				evt.StopPropagation();
				return;
			}

			if (_scrubbing)
			{
				if (evt.pointerId != _scrubPointerId)
					return;

				float t = LocalXToTimeTimeline(evt.localPosition.x);
				playheadTimeSec = t;

				ScrubEnded?.Invoke(t);

				ForceReleaseScrubPointer(sendEndEvent: false);

				MarkDirtyRepaint();
				evt.StopPropagation();
				return;
			}

			if (_rangeSelecting && evt.pointerId == _rangePointerId)
			{
				if (!_rangeMoved)
				{
					hasLoopRange = false;
					LoopRangeCleared?.Invoke();
				}
				else
				{
					float start = Mathf.Min(_rangeA, _rangeB);
					float end = Mathf.Max(_rangeA, _rangeB);

					hasLoopRange = true;
					loopRangeStartSec = start;
					loopRangeEndSec = end;

					LoopRangeChanged?.Invoke(start, end);
				}

				ForceReleaseRangePointer(sendEndEvent: false);

				MarkDirtyRepaint();
				evt.StopPropagation();
			}
		}

		private void OnPointerCancel(PointerCancelEvent evt)
		{
			ForceReleaseAllPointers(sendEndEvents: true);
			evt.StopPropagation();
		}

		private void ForceReleaseAllPointers(bool sendEndEvents)
		{
			ForceReleaseScrubPointer(sendEndEvents);
			ForceReleaseRangePointer(sendEndEvents);
			ForceReleaseEventPointer(sendEndEvents);
		}

		private void ForceReleaseScrubPointer(bool sendEndEvent)
		{
			if (!_scrubbing)
				return;

			if (sendEndEvent)
				ScrubEnded?.Invoke(playheadTimeSec);

			if (_scrubPointerId >= 0 && PointerCaptureHelper.HasPointerCapture(this, _scrubPointerId))
				PointerCaptureHelper.ReleasePointer(this, _scrubPointerId);

			_scrubPointerId = -1;
			_scrubbing = false;
		}

		private void ForceReleaseRangePointer(bool sendEndEvent)
		{
			if (!_rangeSelecting)
				return;

			if (sendEndEvent)
			{
				if (!_rangeMoved)
				{
					hasLoopRange = false;
					LoopRangeCleared?.Invoke();
				}
				else
				{
					float start = Mathf.Min(_rangeA, _rangeB);
					float end = Mathf.Max(_rangeA, _rangeB);
					hasLoopRange = true;
					loopRangeStartSec = start;
					loopRangeEndSec = end;
					LoopRangeChanged?.Invoke(start, end);
				}
			}

			if (_rangePointerId >= 0 && PointerCaptureHelper.HasPointerCapture(this, _rangePointerId))
				PointerCaptureHelper.ReleasePointer(this, _rangePointerId);

			_rangePointerId = -1;
			_rangeSelecting = false;
			_rangeMoved = false;
		}

		private void ForceReleaseEventPointer(bool sendEndEvent)
		{
			if (!_draggingEvent)
				return;

			if (sendEndEvent && _dragEventIndex >= 0)
			{
				float clipT = TimelineTimeToClipTime(playheadTimeSec);
				EventDragEnded?.Invoke(_dragEventIndex, clipT);
			}

			if (_eventPointerId >= 0 && PointerCaptureHelper.HasPointerCapture(this, _eventPointerId))
				PointerCaptureHelper.ReleasePointer(this, _eventPointerId);

			_eventPointerId = -1;
			_draggingEvent = false;
			_dragEventIndex = -1;
		}

		private float LocalXToTimeTimeline(float localX)
		{
			float len = Mathf.Max(MIN_TIMELINE_LEN, timelineLengthSec);
			float w = Mathf.Max(1f, contentRect.width);

			float x01 = Mathf.Clamp01(localX / w);
			float tSec = x01 * len;

			float frameDur = 1f / Mathf.Max(1, fps);
			int frame = Mathf.RoundToInt(tSec / frameDur);
			tSec = Mathf.Clamp(frame * frameDur, 0f, len);

			return tSec;
		}

		private float LocalXToClipTime(float localX)
		{
			float tlLen = Mathf.Max(MIN_TIMELINE_LEN, timelineLengthSec);
			float clipLen = Mathf.Max(MIN_CLIP_LEN, clipLengthSec);
			float w = Mathf.Max(1f, contentRect.width);

			float x01 = Mathf.Clamp01(localX / w);
			float tlT = x01 * tlLen;

			float clipT = (tlT / tlLen) * clipLen;

			float frameDur = 1f / Mathf.Max(1, fps);
			int frame = Mathf.RoundToInt(clipT / frameDur);
			clipT = Mathf.Clamp(frame * frameDur, 0f, clipLen);

			return clipT;
		}

		private float ClipTimeToTimelineTime(float clipTimeSec)
		{
			float tlLen = Mathf.Max(MIN_TIMELINE_LEN, timelineLengthSec);
			float clipLen = Mathf.Max(MIN_CLIP_LEN, clipLengthSec);

			float clipT = Mathf.Clamp(clipTimeSec, 0f, clipLen);
			float tlT = (clipT / clipLen) * tlLen;

			return Mathf.Clamp(tlT, 0f, tlLen);
		}

		private float TimelineTimeToClipTime(float timelineTimeSec)
		{
			float tlLen = Mathf.Max(MIN_TIMELINE_LEN, timelineLengthSec);
			float clipLen = Mathf.Max(MIN_CLIP_LEN, clipLengthSec);

			float tlT = Mathf.Clamp(timelineTimeSec, 0f, tlLen);
			float clipT = (tlT / tlLen) * clipLen;

			float frameDur = 1f / Mathf.Max(1, fps);
			int frame = Mathf.RoundToInt(clipT / frameDur);
			clipT = Mathf.Clamp(frame * frameDur, 0f, clipLen);

			return clipT;
		}

		private bool TryPickEventIndex(Vector2 localPos, out int picked)
		{
			picked = -1;

			if (!drawEventMarkers || clipEvents == null || clipEvents.Length == 0 || clipLengthSec <= MIN_CLIP_LEN)
				return false;

			if (!IsWithinMarkerBandY(localPos.y))
				return false;

			Rect r = contentRect;
			float clipLen = Mathf.Max(MIN_CLIP_LEN, clipLengthSec);

			int best = -1;
			float bestDx = EVENT_HIT_X_PX;

			for (int i = 0; i < clipEvents.Length; i++)
			{
				AnimationEvent ev = clipEvents[i];
				if (ev == null)
					continue;

				float evClipT = Mathf.Clamp(ev.time, 0f, clipLen);
				float x = Mathf.Lerp(r.xMin, r.xMax, evClipT / clipLen);

				float dx = Mathf.Abs(localPos.x - x);
				if (dx <= bestDx)
				{
					bestDx = dx;
					best = i;
				}
			}

			if (best < 0)
				return false;

			picked = best;
			return true;
		}

		private void UpdateHover(Vector2 localPos)
		{
			if (!drawEventMarkers || clipEvents == null || clipEvents.Length == 0 || clipLengthSec <= MIN_CLIP_LEN)
			{
				ClearHover();
				return;
			}

			if (!IsWithinMarkerBandY(localPos.y))
			{
				ClearHover();
				return;
			}

			Rect r = contentRect;
			float clipLen = Mathf.Max(MIN_CLIP_LEN, clipLengthSec);

			int best = -1;
			float bestDx = EVENT_HIT_X_PX;

			for (int i = 0; i < clipEvents.Length; i++)
			{
				AnimationEvent ev = clipEvents[i];
				if (ev == null)
					continue;

				float evClipT = Mathf.Clamp(ev.time, 0f, clipLen);
				float x = Mathf.Lerp(r.xMin, r.xMax, evClipT / clipLen);

				float dx = Mathf.Abs(localPos.x - x);
				if (dx > bestDx)
					continue;

				bestDx = dx;
				best = i;
			}

			if (best == _hoveredEventIndex)
				return;

			_hoveredEventIndex = best;

			if (_hoveredEventIndex < 0)
			{
				tooltip = DEFAULT_TOOLTIP;
				MarkDirtyRepaint();
				return;
			}

			AnimationEvent e = clipEvents[_hoveredEventIndex];
			tooltip = BuildEventTooltip(e, DEFAULT_TOOLTIP);

			MarkDirtyRepaint();
		}

		private void ClearHover()
		{
			if (_hoveredEventIndex == -1)
				return;

			_hoveredEventIndex = -1;
			tooltip = DEFAULT_TOOLTIP;
			MarkDirtyRepaint();
		}

		private bool IsWithinMarkerBandY(float localY)
		{
			Rect r = contentRect;
			float yMin = r.yMin + EVENT_MARKER_TOP_PX - EVENT_HIT_Y_PAD_PX;
			float yMax = r.yMin + EVENT_MARKER_BOTTOM_PX + EVENT_HIT_Y_PAD_PX;
			return localY >= yMin && localY <= yMax;
		}
		
		private static string BuildEventTooltip(AnimationEvent e, string fallback)
		{
			if (e == null) return fallback;

			string param = "(no params)";

			if (e.objectReferenceParameter != null)
				param = $"Object: {e.objectReferenceParameter.name}";
			else if (!string.IsNullOrEmpty(e.stringParameter))
				param = $"String: \"{e.stringParameter}\"";
			else if (!Mathf.Approximately(e.floatParameter, 0f))
				param = $"Float: {e.floatParameter:0.###}";
			else if (e.intParameter != 0)
				param = $"Int: {e.intParameter}";

			return $"Event: {e.functionName}\nTime: {e.time:0.###}s\n{param}";
		}
	}
}

