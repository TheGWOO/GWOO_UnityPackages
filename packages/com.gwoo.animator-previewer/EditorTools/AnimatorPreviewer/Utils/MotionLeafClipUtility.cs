using UnityEditor.Animations;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	internal static class MotionLeafClipUtility
	{
		internal static bool TryGetFirstLeafClip(Motion motion, out AnimationClip clip)
		{
			clip = null;

			if (motion is AnimationClip direct)
			{
				clip = direct;
				return true;
			}

			if (motion is not BlendTree blendTree)
				return false;
			
			ChildMotion[] children = blendTree.children;
			if (children == null || children.Length == 0)
				return false;

			for (int i = 0; i < children.Length; i++)
			{
				if (TryGetFirstLeafClip(children[i].motion, out clip) && clip != null)
					return true;
			}

			return false;
		}
	}
}
