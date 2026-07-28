using UnityEngine;

namespace GWOO.Editor.Tools
{
	public struct AnimatorPreviewerStateEntry
	{
		public string fullPath; // "Layer/SubSM/State"
		public string layerName;
		public string leafName;
		public string normalizedSearchKey;
		public int layerIndex;
		public int stateHash; // Animator.StringToHash("Layer.SubSM.State")
		public Motion motion;
	}
}
