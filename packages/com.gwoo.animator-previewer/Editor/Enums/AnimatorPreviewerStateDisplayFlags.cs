using System;

namespace GWOO.Editor.Tools
{
	[Flags]
	public enum AnimatorPreviewerStateDisplayFlags
	{
		None = 0,
		ShowLayerTag = 1 << 0,
		ShowFullPath = 1 << 1
	}
}
