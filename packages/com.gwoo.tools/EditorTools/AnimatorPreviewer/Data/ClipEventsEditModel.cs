using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GWOO.Editor.Tools
{
	internal sealed class ClipEventsEditModel
	{
		public AnimationClip animationClip;

		public AnimationEvent[] assetEvents = Array.Empty<AnimationEvent>();
		public AnimationEvent[] workingEvents = Array.Empty<AnimationEvent>();

		/// <summary>Index in <see cref="workingEvents"/>.</summary>
		public int SelectedEventIndex { get; set; } = -1;

		public bool Dirty { get; private set; }

		/// <summary>Sorted mapping: row index -> event index in <see cref="workingEvents"/>.</summary>
		public readonly List<int> rowToEventIndex = new();

		// Persist "chosen param kind" even when values look like defaults (0/empty/null).
		private readonly Dictionary<int, ClipEventParamType> _paramKindOverrideByKey = new();

		public bool HasValidSelection => SelectedEventIndex >= 0 && SelectedEventIndex < workingEvents.Length;

		public void SetClip(AnimationClip clip)
		{
			if (animationClip == clip) return;

			animationClip = clip;
			assetEvents = Array.Empty<AnimationEvent>();
			workingEvents = Array.Empty<AnimationEvent>();
			SelectedEventIndex = -1;
			Dirty = false;

			_paramKindOverrideByKey.Clear();
			rowToEventIndex.Clear();
		}

		public void LoadFromAsset(AnimationEvent[] eventsFromAsset)
		{
			assetEvents = CloneEvents(eventsFromAsset);
			workingEvents = CloneEvents(assetEvents);
			Dirty = false;

			_paramKindOverrideByKey.Clear();
			SelectedEventIndex = Mathf.Clamp(SelectedEventIndex, -1, workingEvents.Length - 1);
		}

		public void RecomputeDirty()
		{
			Dirty = ComputeDirty(assetEvents, workingEvents);
		}

		public void BuildSortedRowMap()
		{
			rowToEventIndex.Clear();
			for (int i = 0; i < workingEvents.Length; i++)
				rowToEventIndex.Add(i);

			rowToEventIndex.Sort((a, b) =>
			{
				float ta = workingEvents[a].time;
				float tb = workingEvents[b].time;

				int c = ta.CompareTo(tb);
				if (c != 0) return c;

				string fa = workingEvents[a].functionName ?? string.Empty;
				string fb = workingEvents[b].functionName ?? string.Empty;

				c = string.Compare(fa, fb, StringComparison.OrdinalIgnoreCase);
				if (c != 0) return c;

				// stable by index
				return a.CompareTo(b);
			});

			if (HasValidSelection)
				SelectedEventIndex = Mathf.Clamp(SelectedEventIndex, 0, workingEvents.Length - 1);
		}

		public ClipEventParamType GetParamKind(AnimationEvent e)
		{
			if (e == null) return ClipEventParamType.None;

			int key = ComputeKey(e);
			if (_paramKindOverrideByKey.TryGetValue(key, out ClipEventParamType kOverride))
				return kOverride;

			return InferKind(e);
		}

		public void SetFunctionName(int eventIndex, string newFn)
		{
			if (!IsValidIndex(eventIndex)) return;

			AnimationEvent e = workingEvents[eventIndex];
			int oldKey = ComputeKey(e);

			e.functionName = newFn ?? string.Empty;

			MigrateOverride(oldKey, ComputeKey(e));
		}

		public void SetTime(int eventIndex, float t)
		{
			if (!IsValidIndex(eventIndex)) return;
			workingEvents[eventIndex].time = t;
		}

		public void SetParamKind(int eventIndex, ClipEventParamType type)
		{
			if (!IsValidIndex(eventIndex)) return;

			AnimationEvent e = workingEvents[eventIndex];
			int oldKey = ComputeKey(e);

			ClearParams(e);

			switch (type)
			{
				case ClipEventParamType.Int: e.intParameter = 1; break;
				case ClipEventParamType.Float: e.floatParameter = 1f; break;
				case ClipEventParamType.String: e.stringParameter = "value"; break;
				case ClipEventParamType.Object: break;
			}

			_paramKindOverrideByKey.Remove(oldKey);
			_paramKindOverrideByKey[ComputeKey(e)] = type;
		}

		public void SetInt(int eventIndex, int v)
		{
			if (!IsValidIndex(eventIndex)) return;

			AnimationEvent e = workingEvents[eventIndex];
			int oldKey = ComputeKey(e);

			e.intParameter = v;

			MigrateOverride(oldKey, ComputeKey(e));
		}

		public void SetFloat(int eventIndex, float v)
		{
			if (!IsValidIndex(eventIndex)) return;

			AnimationEvent e = workingEvents[eventIndex];
			int oldKey = ComputeKey(e);

			e.floatParameter = v;

			MigrateOverride(oldKey, ComputeKey(e));
		}

		public void SetString(int eventIndex, string v)
		{
			if (!IsValidIndex(eventIndex)) return;

			AnimationEvent e = workingEvents[eventIndex];
			int oldKey = ComputeKey(e);

			e.stringParameter = v ?? string.Empty;

			MigrateOverride(oldKey, ComputeKey(e));
		}

		public void SetObject(int eventIndex, Object obj)
		{
			if (!IsValidIndex(eventIndex)) return;

			AnimationEvent e = workingEvents[eventIndex];
			int oldKey = ComputeKey(e);

			e.objectReferenceParameter = obj;

			MigrateOverride(oldKey, ComputeKey(e));
		}

		public int AddEvent(float clipTime, string defaultFn)
		{
			List<AnimationEvent> list = new(workingEvents);

			AnimationEvent ev = new()
			{
				time = clipTime,
				functionName = string.IsNullOrEmpty(defaultFn) ? "OnAnimEvent" : defaultFn,
				floatParameter = 0f,
				intParameter = 0,
				stringParameter = string.Empty,
				objectReferenceParameter = null
			};

			list.Add(ev);
			workingEvents = list.ToArray();

			return workingEvents.Length - 1;
		}

		public void DeleteEvent(int eventIndex)
		{
			if (!IsValidIndex(eventIndex)) return;

			List<AnimationEvent> list = new(workingEvents);
			list.RemoveAt(eventIndex);

			workingEvents = list.ToArray();
			SelectedEventIndex = Mathf.Clamp(SelectedEventIndex, -1, workingEvents.Length - 1);
		}

		public EventSignature CaptureSelectionSignature()
		{
			return HasValidSelection ? new EventSignature(workingEvents[SelectedEventIndex]) : default;
		}

		public void RestoreSelectionFromSignature(bool hadSelection, EventSignature sig)
		{
			if (!hadSelection)
			{
				SelectedEventIndex = Mathf.Clamp(SelectedEventIndex, -1, workingEvents.Length - 1);
				return;
			}

			SelectedEventIndex = FindBestMatchIndex(workingEvents, sig);
			SelectedEventIndex = Mathf.Clamp(SelectedEventIndex, -1, workingEvents.Length - 1);
		}

		public float SnapClipTime(float raw, int fps)
		{
			if (animationClip == null) return 0f;

			int f = Mathf.Max(1, fps);

			float clipLen = Mathf.Max(1e-6f, animationClip.length);
			float t = Mathf.Clamp(raw, 0f, clipLen);

			float frameDur = 1f / f;
			int frame = Mathf.RoundToInt(t / frameDur);
			return Mathf.Clamp(frame * frameDur, 0f, clipLen);
		}

		private void MigrateOverride(int oldKey, int newKey)
		{
			if (oldKey == newKey) return;

			if (!_paramKindOverrideByKey.TryGetValue(oldKey, out ClipEventParamType k))
				return;
			
			_paramKindOverrideByKey.Remove(oldKey);
			_paramKindOverrideByKey[newKey] = k;
		}

		private bool IsValidIndex(int idx) => idx >= 0 && idx < workingEvents.Length;

		private static ClipEventParamType InferKind(AnimationEvent e)
		{
			if (e.objectReferenceParameter != null) return ClipEventParamType.Object;
			if (!string.IsNullOrEmpty(e.stringParameter)) return ClipEventParamType.String;
			if (!Mathf.Approximately(e.floatParameter, 0f)) return ClipEventParamType.Float;
			if (e.intParameter != 0) return ClipEventParamType.Int;
			return ClipEventParamType.None;
		}

		private static void ClearParams(AnimationEvent e)
		{
			e.intParameter = 0;
			e.floatParameter = 0f;
			e.stringParameter = string.Empty;
			e.objectReferenceParameter = null;
		}

		private static int ComputeKey(AnimationEvent e)
		{
			unchecked
			{
				int h = 17;
				h = (h * 31) + (e.functionName?.GetHashCode() ?? 0);
				h = (h * 31) + (e.stringParameter?.GetHashCode() ?? 0);
				h = (h * 31) + e.intParameter.GetHashCode();
				h = (h * 31) + e.floatParameter.GetHashCode();
				h = (h * 31) + (e.objectReferenceParameter ? e.objectReferenceParameter.GetInstanceID() : 0);
				return h;
			}
		}

		private static bool ComputeDirty(AnimationEvent[] assetEvents, AnimationEvent[] workingEvents)
		{
			assetEvents ??= Array.Empty<AnimationEvent>();
			workingEvents ??= Array.Empty<AnimationEvent>();

			if (assetEvents.Length != workingEvents.Length)
				return true;

			for (int i = 0; i < assetEvents.Length; i++)
			{
				if (!EventEquals(assetEvents[i], workingEvents[i]))
					return true;
			}

			return false;
		}

		private static bool EventEquals(AnimationEvent a, AnimationEvent b)
		{
			if (a == null || b == null) return a == b;

			if (!Mathf.Approximately(a.time, b.time)) return false;

			if (!string.Equals(a.functionName ?? "", b.functionName ?? "", StringComparison.Ordinal)) return false;
			if (!string.Equals(a.stringParameter ?? "", b.stringParameter ?? "", StringComparison.Ordinal)) return false;

			if (!Mathf.Approximately(a.floatParameter, b.floatParameter)) return false;
			if (a.intParameter != b.intParameter) return false;

			int ao = a.objectReferenceParameter ? a.objectReferenceParameter.GetInstanceID() : 0;
			int bo = b.objectReferenceParameter ? b.objectReferenceParameter.GetInstanceID() : 0;
			if (ao != bo) return false;

			return a.messageOptions == b.messageOptions;
		}

		private static int FindBestMatchIndex(AnimationEvent[] events, EventSignature sig)
		{
			if (events == null || events.Length == 0) return -1;

			const float eps = 0.00025f;

			for (int i = 0; i < events.Length; i++)
			{
				AnimationEvent e = events[i];
				if (e == null) continue;

				if (Mathf.Abs(e.time - sig.time) > eps) continue;
				if (!string.Equals(e.functionName ?? string.Empty, sig.functionName ?? string.Empty, StringComparison.Ordinal)) continue;
				if (e.intParameter != sig.intParameter) continue;
				if (Mathf.Abs(e.floatParameter - sig.floatParameter) > eps) continue;
				if (!string.Equals(e.stringParameter ?? string.Empty, sig.stringParameter ?? string.Empty, StringComparison.Ordinal)) continue;

				int a = e.objectReferenceParameter ? e.objectReferenceParameter.GetInstanceID() : 0;
				if (a != sig.objectId) continue;

				return i;
			}

			// Fallback: nearest time.
			float best = float.PositiveInfinity;
			int bestIdx = -1;

			for (int i = 0; i < events.Length; i++)
			{
				AnimationEvent e = events[i];
				if (e == null) continue;

				float d = Mathf.Abs(e.time - sig.time);
				if (d < best)
				{
					best = d;
					bestIdx = i;
				}
			}

			return bestIdx;
		}

		private static AnimationEvent[] CloneEvents(AnimationEvent[] src)
		{
			if (src == null || src.Length == 0)
				return Array.Empty<AnimationEvent>();

			AnimationEvent[] dst = new AnimationEvent[src.Length];
			for (int i = 0; i < src.Length; i++)
			{
				AnimationEvent e = src[i];
				dst[i] = new AnimationEvent
				{
					functionName = e.functionName,
					time = e.time,
					stringParameter = e.stringParameter,
					floatParameter = e.floatParameter,
					intParameter = e.intParameter,
					objectReferenceParameter = e.objectReferenceParameter,
					messageOptions = e.messageOptions
				};
			}
			return dst;
		}
	}
}

