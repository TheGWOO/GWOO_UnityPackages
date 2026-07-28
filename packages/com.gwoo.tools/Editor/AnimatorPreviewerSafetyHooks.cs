using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWOO.Editor.Tools
{
	[InitializeOnLoad]
	internal static class AnimatorPreviewerSafetyHooks
	{
		private static readonly List<WeakReference<AnimatorPreviewerWindow>> PREVIEWERS = new();
		private static bool _installed;

		static AnimatorPreviewerSafetyHooks()
		{
			Install();
		}

		internal static void Register(AnimatorPreviewerWindow previewerWindow)
		{
			if (previewerWindow == null)
				return;

			Install();
			CleanupDead();

			for (int i = 0; i < PREVIEWERS.Count; i++)
			{
				if (PREVIEWERS[i].TryGetTarget(out AnimatorPreviewerWindow targetPreviewer)
				    && ReferenceEquals(targetPreviewer, previewerWindow))
					return;
			}

			PREVIEWERS.Add(new WeakReference<AnimatorPreviewerWindow>(previewerWindow));
		}

		internal static void Unregister(AnimatorPreviewerWindow previewerWindow)
		{
			if (previewerWindow == null)
				return;

			for (int i = PREVIEWERS.Count - 1; i >= 0; i--)
			{
				if (!PREVIEWERS[i].TryGetTarget(out AnimatorPreviewerWindow registeredPreviewer)
				    || registeredPreviewer == null
				    || ReferenceEquals(registeredPreviewer, previewerWindow))
					PREVIEWERS.RemoveAt(i);
			}
		}

		private static void Install()
		{
			if (_installed)
				return;

			_installed = true;

			// TODO: find a better way to save clip edits before recompilation
			// AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

			PrefabStage.prefabStageOpened += OnPrefabStageChanged;
			PrefabStage.prefabStageClosing += OnPrefabStageChanged;

			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

			EditorSceneManager.sceneSaving += OnSceneSaving;
			EditorSceneManager.sceneSaved += OnSceneSaved;

			EditorApplication.quitting += OnQuitting;
		}

		private static void CleanupDead()
		{
			for (int i = PREVIEWERS.Count - 1; i >= 0; i--)
			{
				if (!PREVIEWERS[i].TryGetTarget(out AnimatorPreviewerWindow p) || p == null)
					PREVIEWERS.RemoveAt(i);
			}
		}

		private static void ForEachPreviewer(Action<AnimatorPreviewerWindow> action)
		{
			CleanupDead();

			for (int i = PREVIEWERS.Count - 1; i >= 0; i--)
			{
				if (!PREVIEWERS[i].TryGetTarget(out AnimatorPreviewerWindow p) || p == null)
					continue;
				
				try { action(p); }
				catch { /* never throw from global hooks */ }
			}
		}

		private static void ForEachBoundInScene(Scene scene, Action<AnimatorPreviewerWindow> action)
		{
			ForEachPreviewer(previewer =>
			{
				if (!previewer.IsBound)
					return;

				Animator targetAnimator = previewer.TargetAnimator;
				if (targetAnimator == null)
					return;

				if (targetAnimator.gameObject.scene != scene)
					return;

				action(previewer);
			});
		}

		private static void ForceUnbindAll(string reason, bool clearAnimatorField = false)
		{
			ForEachPreviewer(previewer => previewer.SafetyUnbind(reason, clearAnimatorField));
		}

		// ----------------- Unity hooks -----------------

		private static void OnBeforeAssemblyReload()
		{
			ForceUnbindAll("beforeAssemblyReload");
		}

		private static void OnPrefabStageChanged(PrefabStage _)
		{
			ForceUnbindAll("prefabStageChanged", clearAnimatorField: true);
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode)
				ForceUnbindAll($"playMode:{state}");
		}

		private static void OnSceneSaving(Scene scene, string path)
		{
			ForEachBoundInScene(scene, previewer =>
				previewer.SafetyRestorePoseSnapshot($"Saving {scene.name} at path {path}"));
		}

		private static void OnSceneSaved(Scene scene)
		{
			ForEachBoundInScene(scene, previewer =>
				previewer.SafetyRestorePreview($"Saved {scene.name}"));
		}

		private static void OnQuitting()
		{
			ForceUnbindAll("quitting");
		}
	}
}

