using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Safe animation events IO for both regular clips and model-imported clips (FBX/DAE/OBJ/BLEND).
	/// </summary>
	internal static class ClipEventsUtility
	{
		internal static AnimationEvent[] GetClipEventsSafe(AnimationClip clip)
		{
			if (clip == null) return Array.Empty<AnimationEvent>();
			try { return clip.events ?? Array.Empty<AnimationEvent>(); }
			catch { return Array.Empty<AnimationEvent>(); }
		}

		internal static AnimationClip TryRefreshClipReference(AnimationClip clip)
		{
			if (clip == null)
				return null;

			string path = AssetDatabase.GetAssetPath(clip);
			if (string.IsNullOrEmpty(path) || !IsModelAssetPath(path))
				return clip;

			Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
			if (all == null || all.Length == 0)
				return clip;

			string wantExact = clip.name;
			string want0 = NormalizeClipName(clip.name);
			string want1 = NormalizeClipName(StripAfterPipe(clip.name));

			// Exact name first.
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i] is AnimationClip c && string.Equals(c.name, wantExact, StringComparison.Ordinal))
					return c;
			}

			// Normalized match.
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i] is not AnimationClip c)
					continue;

				string n = NormalizeClipName(c.name);
				if (n == want0 || n == want1)
					return c;
			}

			// Fuzzy contains.
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i] is not AnimationClip c)
					continue;

				string n = NormalizeClipName(c.name);

				if (n.Contains(want0) || n.Contains(want1) || want0.Contains(n) || want1.Contains(n))
					return c;
			}

			return clip;
		}
		
		internal static bool TryApplyClipEvents(AnimationClip clip, AnimationEvent[] eventsToWrite, string undoLabel, out AnimationClip refreshedClip)
		{
			refreshedClip = null;

			ClipEventsApplyFailure failure = TrySetEventsSafe(clip, eventsToWrite, undoLabel);
			if (failure != ClipEventsApplyFailure.None)
				return false;

			refreshedClip = TryRefreshClipReference(clip);
			return true;
		}

		internal static ClipEventsApplyFailure TrySetEventsSafe(
			AnimationClip clip,
			AnimationEvent[] eventsSeconds,
			string undoLabel)
		{
			if (clip == null)
				return ClipEventsApplyFailure.NullClip;

			string path = AssetDatabase.GetAssetPath(clip);
			bool isModelClip = !string.IsNullOrEmpty(path) && IsModelAssetPath(path);

			eventsSeconds ??= Array.Empty<AnimationEvent>();
			undoLabel = string.IsNullOrEmpty(undoLabel) ? "Apply Clip Events" : undoLabel;

			if (isModelClip)
			{
				bool ok = TrySetModelClipEvents(path, clip, eventsSeconds, undoLabel);
				if (ok)
					return ClipEventsApplyFailure.None;

				Debug.LogWarning(
					$"[AnimatorPreviewer] Failed to apply events via ModelImporter for model clip '{clip.name}' at '{path}'. " +
					"Aborting apply to avoid editor crash.");

				return ClipEventsApplyFailure.ModelImporterFailed;
			}

			try
			{
				Undo.RegisterCompleteObjectUndo(clip, undoLabel);
				AnimationUtility.SetAnimationEvents(clip, eventsSeconds);
				EditorUtility.SetDirty(clip);
				return ClipEventsApplyFailure.None;
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
				Debug.LogWarning($"[AnimatorPreviewer] SetAnimationEvents failed for clip '{clip.name}'.");
				return ClipEventsApplyFailure.SetAnimationEventsFailed;
			}
		}

		private static bool IsModelAssetPath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return false;

			return path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
			       || path.EndsWith(".dae", StringComparison.OrdinalIgnoreCase)
			       || path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)
			       || path.EndsWith(".blend", StringComparison.OrdinalIgnoreCase);
		}

		private static bool TrySetModelClipEvents(string modelPath, AnimationClip clip, AnimationEvent[] eventsSeconds, string undoLabel)
		{
			AssetImporter ai = AssetImporter.GetAtPath(modelPath);
			if (ai is not ModelImporter mi)
				return false;

			ModelImporterClipAnimation[] source = mi.clipAnimations;
			if (source == null || source.Length == 0)
				source = mi.defaultClipAnimations;

			if (source == null || source.Length == 0)
				return false;

			ModelImporterClipAnimation[] working = (ModelImporterClipAnimation[])source.Clone();

			int idx = FindBestClipIndex(working, clip.name);
			if (idx < 0 || idx >= working.Length)
				return false;

			float durationSec = ComputeImporterClipDurationSeconds(working[idx], clip);
			durationSec = Mathf.Max(1e-6f, durationSec);

			bool importerUsesNormalized = GuessImporterStoresNormalizedTime(working[idx].events, durationSec);

			AnimationEvent[] safe = CloneEvents(eventsSeconds);
			for (int i = 0; i < safe.Length; i++)
			{
				if (safe[i] == null) continue;

				if (float.IsNaN(safe[i].time) || float.IsInfinity(safe[i].time))
					safe[i].time = 0f;

				if (safe[i].time < 0f) safe[i].time = 0f;

				Object obj = safe[i].objectReferenceParameter;
				if (obj != null && !EditorUtility.IsPersistent(obj))
					safe[i].objectReferenceParameter = null;
			}

			AnimationEvent[] toWrite = importerUsesNormalized
				? ConvertSecondsToNormalized(safe, durationSec)
				: ClampSeconds(safe, durationSec);

			Undo.RegisterCompleteObjectUndo(mi, undoLabel);

			working[idx].events = toWrite;
			mi.clipAnimations = working;
			EditorUtility.SetDirty(mi);

			try
			{
				mi.SaveAndReimport();
			}
			catch
			{
				AssetDatabase.WriteImportSettingsIfDirty(modelPath);
				AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
			}

			return true;
		}

		private static bool GuessImporterStoresNormalizedTime(AnimationEvent[] existingImporterEvents, float durationSec)
		{
			if (existingImporterEvents != null && existingImporterEvents.Length > 0)
			{
				for (int i = 0; i < existingImporterEvents.Length; i++)
				{
					float t = existingImporterEvents[i].time;
					if (t > 1.001f && t <= durationSec * 1.25f)
						return false;
				}
			}

			return true;
		}

		private static int FindBestClipIndex(ModelImporterClipAnimation[] clips, string clipName)
		{
			if (clips == null || clips.Length == 0) return -1;
			if (string.IsNullOrEmpty(clipName)) return -1;

			string n0 = NormalizeClipName(clipName);
			string n1 = NormalizeClipName(StripAfterPipe(clipName));

			for (int i = 0; i < clips.Length; i++)
			{
				if (string.Equals(clips[i].name, clipName, StringComparison.Ordinal))
					return i;
			}

			for (int i = 0; i < clips.Length; i++)
			{
				string cn = NormalizeClipName(clips[i].name);
				if (cn == n0 || cn == n1)
					return i;
			}

			for (int i = 0; i < clips.Length; i++)
			{
				string cn = NormalizeClipName(clips[i].name);
				if (cn.Contains(n0) || cn.Contains(n1) || n0.Contains(cn) || n1.Contains(cn))
					return i;
			}

			return -1;
		}

		private static string StripAfterPipe(string s)
		{
			if (string.IsNullOrEmpty(s)) return s;
			int idx = s.IndexOf('|');
			return idx >= 0 ? s.Substring(idx + 1) : s;
		}

		private static string NormalizeClipName(string s)
		{
			if (string.IsNullOrEmpty(s)) return string.Empty;
			s = s.Trim().ToLowerInvariant();
			s = s.Replace(" ", "").Replace("_", "").Replace("-", "");
			return s;
		}

		private static float ComputeImporterClipDurationSeconds(ModelImporterClipAnimation clipAnim, AnimationClip clip)
		{
			float fr = (clip != null && clip.frameRate > 1e-3f) ? clip.frameRate : 30f;

			float frames = clipAnim.lastFrame - clipAnim.firstFrame;
			if (frames > 0.01f && frames < 1e7f)
			{
				float sec = frames / fr;
				if (sec > 1e-4f && sec < 36000f)
					return sec;
			}

			if (clip != null && clip.length > 1e-6f)
				return clip.length;

			return 1f;
		}

		private static AnimationEvent[] ClampSeconds(AnimationEvent[] src, float durationSec)
		{
			if (src == null || src.Length == 0)
				return Array.Empty<AnimationEvent>();

			float max = Mathf.Max(0.00001f, durationSec);

			AnimationEvent[] dst = new AnimationEvent[src.Length];
			for (int i = 0; i < src.Length; i++)
			{
				AnimationEvent e = src[i];
				dst[i] = new AnimationEvent
				{
					functionName = e.functionName,
					time = Mathf.Clamp(e.time, 0f, max),
					stringParameter = e.stringParameter,
					floatParameter = e.floatParameter,
					intParameter = e.intParameter,
					objectReferenceParameter = e.objectReferenceParameter,
					messageOptions = e.messageOptions
				};
			}
			return dst;
		}

		private static AnimationEvent[] ConvertSecondsToNormalized(AnimationEvent[] srcSeconds, float durationSec)
		{
			if (srcSeconds == null || srcSeconds.Length == 0)
				return Array.Empty<AnimationEvent>();

			AnimationEvent[] dst = new AnimationEvent[srcSeconds.Length];

			for (int i = 0; i < srcSeconds.Length; i++)
			{
				AnimationEvent e = srcSeconds[i];
				AnimationEvent c = new()
				{
					functionName = e.functionName,
					time = 0f,
					stringParameter = e.stringParameter,
					floatParameter = e.floatParameter,
					intParameter = e.intParameter,
					objectReferenceParameter = e.objectReferenceParameter,
					messageOptions = e.messageOptions
				};

				float t = Mathf.Max(0f, e.time);

				int guard = 0;
				while (t > durationSec * 1.25f && guard++ < 8)
					t /= durationSec;

				float norm = durationSec > 1e-6f ? (t / durationSec) : 0f;
				c.time = Mathf.Clamp01(norm);

				dst[i] = c;
			}

			return dst;
		}

		internal static AnimationEvent[] CloneEvents(AnimationEvent[] src)
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

		internal static void SortEventsStable(AnimationEvent[] events)
		{
			if (events == null || events.Length <= 1)
				return;

			Array.Sort(events, (a, b) =>
			{
				if (a == null || b == null)
					return a == b ? 0 : (a == null ? 1 : -1);

				int c = a.time.CompareTo(b.time);
				if (c != 0) return c;

				string fa = a.functionName ?? string.Empty;
				string fb = b.functionName ?? string.Empty;
				c = string.Compare(fa, fb, StringComparison.OrdinalIgnoreCase);
				if (c != 0) return c;

				c = a.intParameter.CompareTo(b.intParameter);
				if (c != 0) return c;

				float df = a.floatParameter - b.floatParameter;
				if (Mathf.Abs(df) > 0.00025f) return df < 0f ? -1 : 1;

				string sa = a.stringParameter ?? string.Empty;
				string sb = b.stringParameter ?? string.Empty;
				c = string.Compare(sa, sb, StringComparison.Ordinal);
				if (c != 0) return c;

				int ao = a.objectReferenceParameter ? a.objectReferenceParameter.GetInstanceID() : 0;
				int bo = b.objectReferenceParameter ? b.objectReferenceParameter.GetInstanceID() : 0;
				return ao.CompareTo(bo);
			});
		}
	}
}

