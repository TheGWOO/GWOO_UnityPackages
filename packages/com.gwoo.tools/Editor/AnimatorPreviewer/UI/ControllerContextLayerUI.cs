using System;
using System.Collections.Generic;
using GWOO.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Small UI component used by ControllerPreviewPanel to render one layer context block.
	/// Keeps the ControllerPreviewPanel simpler and avoids "public field bags".
	/// </summary>
	internal sealed class ControllerContextLayerUI
	{
		public VisualElement Root { get; }

		private readonly Label _header;
		private readonly Label _current;
		private readonly Label _cycles;

		private readonly VisualElement _currentBarFill;

		private readonly VisualElement _transitionBlock;
		private readonly Label _transition;
		private readonly Label _transitionNorm;

		private readonly VisualElement _transitionBarFill;

		private readonly Label _clipsEmpty;
		private readonly VisualElement _clipsContainer;

		private readonly List<int> _clipIds = new();
		private readonly List<Label> _clipLabels = new();
		private readonly List<CustomButton> _clipPreviewButtons = new();

		private int _layerIndex;
		private float _lastCyclesValue;

		private readonly Color _accentCtrl;
		private readonly Color _accentCtrlShift;

		public ControllerContextLayerUI(AnimatorPreviewerTheme theme)
		{
			_accentCtrl = theme.accentCtrl;
			_accentCtrlShift = theme.accentCtrlShift;

			Root = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					paddingLeft = 10,
					paddingRight = 10,
					paddingTop = 8,
					paddingBottom = 10,
					marginBottom = 8,
					backgroundColor = new StyleColor(new Color(_accentCtrl.r, _accentCtrl.g, _accentCtrl.b, 0.06f)),
					borderBottomLeftRadius = 8,
					borderBottomRightRadius = 8,
					borderTopLeftRadius = 8,
					borderTopRightRadius = 8
				}
			};

			_header = new Label
			{
				pickingMode = PickingMode.Ignore,
				style = { unityFontStyleAndWeight = FontStyle.Bold }
			};
			Root.Add(_header);

			_current = new Label { pickingMode = PickingMode.Ignore };
			Root.Add(_current);

			_cycles = new Label { pickingMode = PickingMode.Ignore };
			Root.Add(_cycles);

			BuildProgressBar(Root, _accentCtrl, out _, out _currentBarFill);

			_transitionBlock = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					marginTop = 2
				}
			};
			Root.Add(_transitionBlock);

			_transition = new Label { pickingMode = PickingMode.Ignore };
			_transitionBlock.Add(_transition);

			_transitionNorm = new Label { pickingMode = PickingMode.Ignore };
			_transitionBlock.Add(_transitionNorm);

			BuildProgressBar(_transitionBlock, _accentCtrlShift, out _, out _transitionBarFill);

			_clipsEmpty = new Label("Clip: (none)")
			{
				pickingMode = PickingMode.Ignore,
				style = { marginTop = 4 }
			};
			Root.Add(_clipsEmpty);

			_clipsContainer = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					marginTop = 2
				}
			};
			Root.Add(_clipsContainer);

			SetPulseOpacity(0.90f);
		}

		public void Update(
			IAnimatorPreviewerHost host,
			AnimatorPreviewerControllerLayerContext ctx,
			Action<int, int, float> onPreviewClip)
		{
			if (host == null)
				return;

			_layerIndex = ctx.layerIndex;
			_lastCyclesValue = ctx.currentNormalized;

			_header.text = $"{ctx.layerIndex}: {ctx.layerName}";

			string curName = host.ResolveStateName(ctx.layerIndex, ctx.currentStateHash);
			_current.text = $"Current: {curName}";
			_cycles.text = $"Cycles: {ctx.currentNormalized:0.###}";

			UpdateProgressBar(_currentBarFill, Repeat01(ctx.currentNormalized));

			if (ctx.inTransition)
			{
				string nextName = host.ResolveStateName(ctx.layerIndex, ctx.nextStateHash);

				_transitionBlock.style.display = DisplayStyle.Flex;
				_transition.text = $"Transition → {nextName}";
				_transitionNorm.text = $"Transition normalized: {ctx.transitionNormalized:0.###}";

				UpdateProgressBar(_transitionBarFill, Mathf.Clamp01(ctx.transitionNormalized));
			}
			else
			{
				_transitionBlock.style.display = DisplayStyle.None;
			}

			AnimatorPreviewerControllerClipInfo[] clips = ctx.clips;
			if (clips == null || clips.Length == 0)
			{
				_clipsEmpty.style.display = DisplayStyle.Flex;
				_clipsContainer.style.display = DisplayStyle.None;
				return;
			}

			_clipsEmpty.style.display = DisplayStyle.None;
			_clipsContainer.style.display = DisplayStyle.Flex;

			EnsureClipRows(host, clips.Length, onPreviewClip);

			// Update ids + labels (no rebuild needed for reordered content as long as count stays).
			for (int i = 0; i < clips.Length; i++)
			{
				AnimatorPreviewerControllerClipInfo c = clips[i];

				_clipIds[i] = c.clipId;
				_clipLabels[i].text = $"Clip: {c.clipName} | w {c.weight:0.###}";
			}
		}

		public void SetPulseOpacity(float opacity)
		{
			if (_currentBarFill != null)
				_currentBarFill.style.opacity = opacity;

			if (_transitionBarFill != null)
				_transitionBarFill.style.opacity = opacity;
		}

		private void EnsureClipRows(IAnimatorPreviewerHost host, int count, Action<int, int, float> onPreviewClip)
		{
			// Grow
			while (_clipLabels.Count < count)
			{
				int rowIndex = _clipLabels.Count;

				VisualElement row = new()
				{
					style =
					{
						flexDirection = FlexDirection.Row,
						alignItems = Align.Center,
						marginTop = 4
					}
				};

				Label l = new()
				{
					pickingMode = PickingMode.Ignore,
					style = { flexGrow = 1f }
				};
				row.Add(l);

				CustomButton preview = new(() =>
				{
					if (rowIndex < 0 || rowIndex >= _clipIds.Count)
						return;

					int clipId = _clipIds[rowIndex];
					if (clipId == 0)
						return;

					onPreviewClip?.Invoke(_layerIndex, clipId, _lastCyclesValue);
				})
				{
					text = "Preview",
					tooltip = "Preview the current clip in ClipMode.",
					Width = 0,
					style =
					{
						minWidth = 80,
						marginLeft = 0,
						marginRight = 0,
						marginTop = 0,
						marginBottom = 0,
						backgroundColor = new StyleColor(host.Theme.accentClip)
					}
				};

				row.Add(preview);

				_clipsContainer.Add(row);

				_clipLabels.Add(l);
				_clipPreviewButtons.Add(preview);
				_clipIds.Add(0);
			}

			// Trim
			while (_clipLabels.Count > count)
			{
				int last = _clipLabels.Count - 1;

				// Row is the parent of label + button.
				VisualElement row = _clipLabels[last]?.parent;
				row?.RemoveFromHierarchy();

				_clipLabels.RemoveAt(last);
				_clipPreviewButtons.RemoveAt(last);
				_clipIds.RemoveAt(last);
			}
		}

		// --- Progress bar helpers ---

		private static void BuildProgressBar(VisualElement parent, Color fillColor, out VisualElement bg, out VisualElement fill)
		{
			bg = new VisualElement
			{
				style =
				{
					height = 6,
					marginTop = 3,
					marginBottom = 2,
					backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.18f)),
					borderBottomLeftRadius = 3,
					borderBottomRightRadius = 3,
					borderTopLeftRadius = 3,
					borderTopRightRadius = 3
				}
			};

			fill = new VisualElement
			{
				style =
				{
					height = 6,
					width = new StyleLength(new Length(0f, LengthUnit.Percent)),
					backgroundColor = new StyleColor(new Color(fillColor.r, fillColor.g, fillColor.b, 0.85f)),
					borderBottomLeftRadius = 3,
					borderBottomRightRadius = 3,
					borderTopLeftRadius = 3,
					borderTopRightRadius = 3
				}
			};

			bg.Add(fill);
			parent.Add(bg);
		}

		private static void UpdateProgressBar(VisualElement fill, float percentage01)
		{
			if (fill == null)
				return;

			float pct = Mathf.Clamp01(percentage01) * 100f;
			fill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
		}

		private static float Repeat01(float t)
		{
			if (float.IsNaN(t) || float.IsInfinity(t))
				return 0f;

			t = t - Mathf.Floor(t);
			if (t < 0f) t += 1f;
			return Mathf.Clamp01(t);
		}
	}
}

