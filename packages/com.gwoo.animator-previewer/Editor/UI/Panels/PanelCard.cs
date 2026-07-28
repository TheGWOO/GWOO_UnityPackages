using System;
using GWOO.UIElements;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal static class PanelCard
	{
		public static VisualElement Spacer(int wOrH, bool horizontal)
		{
			VisualElement s = new();
			if (horizontal) s.style.width = wOrH;
			else s.style.height = wOrH;
			s.style.flexShrink = 0;
			return s;
		}

		public static CustomButton NewToolbarButton(string text, Action onClick, float minWidth)
		{
			return new CustomButton(onClick)
			{
				text = text,
				Width = 0,
				style =
				{
					minWidth = minWidth,
					minHeight = 15,
					height = 18,
					paddingLeft = 0,
					paddingRight = 0,
					paddingTop = 0,
					paddingBottom = 0,
					marginLeft = 0,
					marginRight = 4,
					marginTop = 0,
					marginBottom = 0
				}
			};
		}

		public static CustomButton NewInlineButton(string text, Action onClick, float minWidth)
		{
			return new CustomButton(onClick)
			{
				text = text,
				Width = 0,
				style =
				{
					minWidth = minWidth,
					marginLeft = 0,
					marginRight = 6,
					marginTop = 0,
					marginBottom = 0
				}
			};
		}

		public static CustomButton NewRowButton(string text, Action onClick, float minWidth, float marginRight = 6)
		{
			return new CustomButton(onClick)
			{
				text = text,
				Width = 0,
				style =
				{
					minWidth = minWidth,
					marginLeft = 0,
					marginRight = marginRight,
					marginTop = 0,
					marginBottom = 0
				}
			};
		}

		public static void SetDisplay(VisualElement ve, bool display)
		{
			if (ve == null) return;
			ve.style.display = display ? DisplayStyle.Flex : DisplayStyle.None;
		}

		public static void SetClass(VisualElement visualElement, string className, bool enabled)
		{
			if (visualElement == null)
				return;

			if (enabled) visualElement.AddToClassList(className);
			else visualElement.RemoveFromClassList(className);
		}

		public static void SafelyRemovePanel(this IAnimatorPreviewerPanel panel)
		{
			if (panel?.Root == null)
				return;

			panel.Root.RemoveFromHierarchy();
			panel.Root.Clear();
		}
	}
}



