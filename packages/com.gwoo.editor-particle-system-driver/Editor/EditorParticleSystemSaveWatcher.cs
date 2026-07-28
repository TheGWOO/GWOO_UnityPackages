using UnityEditor;

namespace GWOO.Editor.ParticlePreview
{
	/// <summary>
	/// Project save hook (Save Project / Apply Prefab / etc.).
	/// Called by Unity right before it writes serialized assets or scene files to disk.
	/// </summary>
	internal sealed class EditorParticleSystemSaveWatcher : AssetModificationProcessor
	{
		private static string[] OnWillSaveAssets(string[] paths)
		{
			EditorParticleSystemDriver.NotifyWillSaveAssets(paths);
			return paths;
		}
	}
}
