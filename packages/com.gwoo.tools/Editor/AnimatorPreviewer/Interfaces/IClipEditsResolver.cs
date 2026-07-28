namespace GWOO.Editor.Tools
{
	public interface IClipEditsResolver
	{
		bool TryResolvePendingClipEdits(string context, PendingEditsResolution resolution);
	}
}
