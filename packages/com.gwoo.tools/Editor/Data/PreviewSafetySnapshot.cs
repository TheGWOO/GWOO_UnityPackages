using System.Collections.Generic;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	internal sealed class PreviewSafetySnapshot
	{
		private readonly TransformPoseSnapshot _pose = new();
		private readonly PrefabOverridesSnapshot _prefab = new();

		public bool IsEmpty => _pose.IsEmpty && _prefab.IsEmpty;

		public void Capture(IReadOnlyList<Transform> animatedTransforms)
		{
			_pose.Capture(animatedTransforms);
			_prefab.Capture(animatedTransforms);
		}

		public void RestorePoseAndPrefabOverrides()
		{
			_prefab.Restore();
			_pose.Restore();
		}

		public void Clear()
		{
			_pose.Clear();
			_prefab.Clear();
		}
	}
}
