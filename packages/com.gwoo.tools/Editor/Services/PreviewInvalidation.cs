namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Manages dirty flags for the previewer system to optimize UI and scene updates.
	/// </summary>
	internal sealed class PreviewInvalidation
	{
		private PreviewInvalidationFlags _flags;

		internal void Add(PreviewInvalidationFlags flags)
		{
			_flags |= flags;
		}

		internal PreviewInvalidationFlags Consume()
		{
			PreviewInvalidationFlags consumedFlags = _flags;
			_flags = PreviewInvalidationFlags.None;
			return consumedFlags;
		}

		internal void Clear()
		{
			_flags = PreviewInvalidationFlags.None;
		}
	}
}

