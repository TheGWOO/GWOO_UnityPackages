namespace GWOO.Editor.Tools
{
	internal sealed class PreviewContext
	{
		internal AnimatorPreviewerState State { get; }
		internal AnimatorPreviewerRuntime Runtime { get; }
		internal PreviewInvalidation Invalidation { get; }
		internal PreviewFxBridge FxBridge { get; }
		internal PreviewHub Hub { get; }

		internal PreviewContext(
			AnimatorPreviewerState state,
			AnimatorPreviewerRuntime runtime,
			PreviewInvalidation invalidation,
			PreviewFxBridge fxBridge,
			PreviewHub hub)
		{
			State = state;
			Runtime = runtime;
			Invalidation = invalidation;
			FxBridge = fxBridge;
			Hub = hub;
		}
	}
}

