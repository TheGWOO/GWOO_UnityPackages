using UnityEngine;

namespace GWOO.Editor.Tools
{
	public sealed class AnimatorPreviewerTheme
	{
		public readonly Color accentClip = new(0.16f, 0.62f, 0.42f, 1f);
		public readonly Color accentCtrl = new(0.43f, 0.36f, 0.78f, 1f);
		
		public readonly Color accentClipShift = new(0.65f, 0.7f, 0.4f, 1f);
		public readonly Color accentCtrlShift = new(0.7f, 0.4f, 0.78f, 1f);
		
		public readonly Color pauseOrange = new(0.85f, 0.45f, 0.3f, 1f);
		public readonly Color editWarning = new(0.86f, 0.24f, 0.24f, 1f);

		public readonly Color timelineBg = new(0.13f, 0.135f, 0.14f, 1f);
		public readonly Color timelineBorder = new(0f, 0f, 0f, 0.30f);
		public readonly Color timelineTicks = new(0.36f, 0.36f, 0.36f, 1f);
		public readonly Color playhead = new(0.20f, 1.0f, 0.75f, 1f);

		public readonly Color eventMarker = new(1.0f, 0.78f, 0.25f, 0.95f);
		public readonly Color eventMarkerHover = new(1.0f, 0.92f, 0.35f, 1f);
	}
}

