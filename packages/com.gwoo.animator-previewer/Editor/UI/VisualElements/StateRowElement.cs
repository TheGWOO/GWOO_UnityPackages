using GWOO.UIElements;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class StateRowElement : VisualElement
	{
		private readonly IAnimatorPreviewerHost _host;

		private readonly VisualElement _accent;
		private readonly Label _primary;
		private readonly Label _secondary;
		private readonly Label _layerPill;

		private readonly CustomButton _btnClip;
		private readonly CustomButton _btnCtrl;

		private AnimatorPreviewerStateEntry _state;
		private int _index;

		public StateRowElement(IAnimatorPreviewerHost host)
		{
			_host = host;

			style.flexDirection = FlexDirection.Row;
			style.alignItems = Align.Center;
			style.paddingLeft = 8;
			style.paddingRight = 8;
			style.paddingTop = 6;
			style.paddingBottom = 6;
			style.minHeight = 40;

			_accent = new VisualElement
			{
				style =
				{
					width = 4,
					height = 28,
					marginRight = 10,
					borderTopLeftRadius = 2,
					borderTopRightRadius = 2,
					borderBottomLeftRadius = 2,
					borderBottomRightRadius = 2
				}
			};
			Add(_accent);

			VisualElement textCol = new()
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					flexGrow = 1f,
					flexShrink = 1f,
					marginRight = 8
				}
			};
			Add(textCol);

			VisualElement topLine = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexWrap = Wrap.NoWrap
				}
			};
			textCol.Add(topLine);

			_primary = new Label
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					unityFontStyleAndWeight = FontStyle.Bold,
					flexGrow = 1f,
					flexShrink = 1f,
					unityTextAlign = TextAnchor.MiddleLeft
				}
			};
			topLine.Add(_primary);

			_layerPill = new Label
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					paddingLeft = 8,
					paddingRight = 8,
					paddingTop = 2,
					paddingBottom = 2,
					marginLeft = 6,
					borderTopLeftRadius = 10,
					borderTopRightRadius = 10,
					borderBottomLeftRadius = 10,
					borderBottomRightRadius = 10,
					opacity = 0.9f,
					flexShrink = 0f
				}
			};
			topLine.Add(_layerPill);

			_secondary = new Label
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					opacity = 0.75f,
					fontSize = 11,
					whiteSpace = WhiteSpace.NoWrap,
					overflow = Overflow.Hidden,
					textOverflow = TextOverflow.Ellipsis
				}
			};
			textCol.Add(_secondary);

			VisualElement btnRow = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexShrink = 0f
				}
			};
			Add(btnRow);

			_btnClip = new CustomButton(OnPreviewClipClicked)
			{
				text = "Preview Clip",
				Width = 0,
				style =
				{
					minWidth = 54,
					marginLeft = 0,
					marginRight = 6,
					marginTop = 0,
					marginBottom = 0
				},
				tooltip = "Preview first leaf clip"
			};
			btnRow.Add(_btnClip);

			_btnCtrl = new CustomButton(OnPlayControllerClicked)
			{
				text = "Play Controller",
				Width = 0,
				style =
				{
					minWidth = 54,
					marginLeft = 0,
					marginRight = 0,
					marginTop = 0,
					marginBottom = 0
				},
				tooltip = "Play state via controller"
			};
			btnRow.Add(_btnCtrl);
		}

		private void OnPreviewClipClicked()
		{
			if (_host == null) return;
			if (_state.stateHash == 0) return;

			_host.CmdPreviewSelectedStateClip(_state.motion);
		}

		private void OnPlayControllerClicked()
		{
			if (_host == null) return;
			if (_state.stateHash == 0) return;

			_host.CmdPlaySelectedStateController(_state.layerIndex, _state.stateHash);
		}

		public void Bind(AnimatorPreviewerStateEntry s, int index, AnimatorPreviewerViewState vs, AnimatorPreviewerTheme t)
		{
			_state = s;
			_index = index;

			bool showLayer = (vs.stateDisplayFlags & AnimatorPreviewerStateDisplayFlags.ShowLayerTag) != 0;
			bool showFullPath = (vs.stateDisplayFlags & AnimatorPreviewerStateDisplayFlags.ShowFullPath) != 0;

			string primary = showFullPath ? s.fullPath : s.leafName;
			string motion = GetMotionLabel(s.motion);

			_primary.text = primary;
			_secondary.text = motion;

			tooltip = $"{s.fullPath}\n{motion}";

			Color layerAccent = Color.HSVToRGB(
				Mathf.Repeat((s.layerIndex * 0.23f) + 0.12f, 1f),
				0.55f,
				0.85f);

			_accent.style.backgroundColor = new StyleColor(layerAccent);

			if (showLayer)
			{
				_layerPill.style.display = DisplayStyle.Flex;
				_layerPill.text = s.layerName;
				_layerPill.style.backgroundColor = new StyleColor(new Color(layerAccent.r, layerAccent.g, layerAccent.b, 0.18f));
			}
			else
			{
				_layerPill.style.display = DisplayStyle.None;
			}

			bool selected = (vs.selectedStateLayer == s.layerIndex && vs.selectedStateHash == s.stateHash);
			if (selected)
			{
				Color modeAccent = (vs.mode == AnimatorPreviewerMode.Clip) ? t.accentClip : t.accentCtrl;
				style.backgroundColor = new StyleColor(new Color(modeAccent.r, modeAccent.g, modeAccent.b, 0.16f));
			}
			else
			{
				style.backgroundColor = new StyleColor((_index & 1) == 0
					? new Color(0f, 0f, 0f, 0.05f)
					: Color.clear);
			}

			_btnClip.SetEnabled(vs.isBound);
			_btnCtrl.SetEnabled(vs.isBound);

			_btnClip.style.backgroundColor = new StyleColor(t.accentClip);
			_btnCtrl.style.backgroundColor = new StyleColor(t.accentCtrl);
		}

		private static string GetMotionLabel(Motion motion)
		{
			if (motion == null) return "Motion: (none)";

			return motion switch
			{
				AnimationClip c => $"Clip: {c.name}",
				BlendTree bt => $"BlendTree: {bt.name}",
				_ => $"Motion: {motion.name}"
			};
		}
	}
}

