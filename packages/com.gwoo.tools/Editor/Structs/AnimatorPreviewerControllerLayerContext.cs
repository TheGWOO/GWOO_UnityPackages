namespace GWOO.Editor.Tools
{
	public readonly struct AnimatorPreviewerControllerLayerContext
	{
		public readonly int layerIndex;
		public readonly string layerName;

		public readonly int currentStateHash;
		public readonly float currentNormalized;

		public readonly bool inTransition;
		public readonly int nextStateHash;
		public readonly float transitionNormalized;

		public readonly AnimatorPreviewerControllerClipInfo[] clips;

		public AnimatorPreviewerControllerLayerContext(
			int layerIndex, string layerName,
			int currentStateHash, float currentNormalized,
			bool inTransition, int nextStateHash, float transitionNormalized,
			AnimatorPreviewerControllerClipInfo[] clips)
		{
			this.layerIndex = layerIndex;
			this.layerName = layerName;
			this.currentStateHash = currentStateHash;
			this.currentNormalized = currentNormalized;

			this.inTransition = inTransition;
			this.nextStateHash = nextStateHash;
			this.transitionNormalized = transitionNormalized;

			this.clips = clips;
		}
	}
}
