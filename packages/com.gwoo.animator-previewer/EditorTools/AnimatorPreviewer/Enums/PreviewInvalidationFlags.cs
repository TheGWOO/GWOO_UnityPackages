using System;

namespace GWOO.Editor.Tools
{
	[Flags]
	internal enum PreviewInvalidationFlags
	{
		None = 0,

		Header = 1 << 0,

		// Clip mode
		Timeline = 1 << 1,

		// Controller mode
		ControllerContext = 1 << 2,
		RightPanelParams = 1 << 3,

		// Structural changes (mode switch, rebind, clip rebuilt, state list changed...)
		FullUI = 1 << 4,

		// SceneView needs repaint
		Scene = 1 << 5,
		
		Playback = 1 << 6,
		
		RightPanelStates = 1 << 7,
	}
}
