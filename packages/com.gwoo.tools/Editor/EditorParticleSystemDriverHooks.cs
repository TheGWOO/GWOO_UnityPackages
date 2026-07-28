using UnityEditor;
using UnityEditor.SceneManagement;
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;

namespace GWOO.Editor.ParticlePreview
{
	/// <summary>
	/// Single responsibility: wire Unity Editor lifecycle/save events to the session.
	/// </summary>
	internal static class EditorParticleSystemDriverHooks
	{
		public static void Register(ParticlePreviewSession session)
		{
			if (session == null) return;

			EditorApplication.playModeStateChanged += session.OnPlayModeStateChanged;
			EditorApplication.quitting += session.OnEditorQuitting;

			AssemblyReloadEvents.beforeAssemblyReload += session.OnBeforeAssemblyReload;

			EditorSceneManager.sceneSaving += session.OnSceneSaving;
			EditorSceneManager.sceneSaved += session.OnSceneSaved;

			PrefabStage.prefabSaving += session.OnPrefabStageSaving;
			PrefabStage.prefabSaved += session.OnPrefabStageSaved;
		}
	}
}
