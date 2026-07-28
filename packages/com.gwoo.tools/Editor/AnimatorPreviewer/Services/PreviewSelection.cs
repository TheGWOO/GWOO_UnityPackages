using UnityEditor;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	internal static class PreviewSelection
	{
		internal static bool TryGetSelectionAnimator(out Animator animator)
		{
			animator = null;

			GameObject go = Selection.activeGameObject;
			if (go == null)
				return false;

			animator = go.GetComponentInParent<Animator>();
			return animator != null;
		}
	}
}
