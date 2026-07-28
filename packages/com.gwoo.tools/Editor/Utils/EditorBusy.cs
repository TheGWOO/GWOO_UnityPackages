using UnityEditor;

namespace GWOO.Editor.Tools
{
	internal static class EditorBusy
	{
		private static int _depth;

		internal static void Push()
		{
			_depth++;
		}

		internal static void Pop()
		{
			if (_depth > 0) _depth--;
		}

		internal static void Reset()
		{
			_depth = 0;
		}

		internal static bool IsBusy()
		{
			return EditorApplication.isUpdating
			       || EditorApplication.isPlayingOrWillChangePlaymode
			       || _depth > 0;
		}
	}
}
