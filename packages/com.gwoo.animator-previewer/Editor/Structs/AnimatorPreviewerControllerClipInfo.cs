namespace GWOO.Editor.Tools
{
	public readonly struct AnimatorPreviewerControllerClipInfo
	{
		public readonly int clipId;
		public readonly string clipName;
		public readonly float weight;

		public AnimatorPreviewerControllerClipInfo(int clipId, string clipName, float weight)
		{
			this.clipId = clipId;
			this.clipName = clipName;
			this.weight = weight;
		}
	}
}
