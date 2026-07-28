using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Simple, consistent lifecycle for Previewer UI pieces:
	/// - Build once
	/// - Refresh often
	/// - Dispose to unhook callbacks / release references
	/// </summary>
	public interface IAnimatorPreviewerPanel
	{
		VisualElement Root { get; }

		void Build(VisualElement parent, IAnimatorPreviewerHost host);
		void Refresh(IAnimatorPreviewerHost host);
		void SetVisible(bool visible);
		void Dispose();
	}
}
