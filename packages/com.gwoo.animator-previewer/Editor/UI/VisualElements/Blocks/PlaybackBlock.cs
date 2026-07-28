using System;
using GWOO.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Reusable play/pause + speed (+ optional loop) UI block.
	/// Caller owns actual playback logic; this block only emits commands.
	/// </summary>
	internal sealed class PlaybackBlock : VisualElement, IDisposable
	{
		private const float SLIDER_MAX = 2f;
		private const float FIELD_MAX = 3f;

		private readonly bool _showLoop;

		private bool _ignore;
		private bool _disposed;

		private CustomButton _playPauseBtn;
		private Slider _speedSlider;
		private FloatField _speedField;
		private Toggle _loopToggle;

		private readonly CallbackScope _callbacks = new();

		public event Action OnPlayPause;
		public event Action<float> OnSpeedChanged;
		public event Action<bool> OnLoopChanged;

		public PlaybackBlock(bool showLoop)
		{
			_showLoop = showLoop;
			BuildUI();
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			_callbacks.Clear();

			OnPlayPause = null;
			OnSpeedChanged = null;
			OnLoopChanged = null;

			_playPauseBtn = null;
			_speedSlider = null;
			_speedField = null;
			_loopToggle = null;
		}

		private void BuildUI()
		{
			style.flexDirection = FlexDirection.Column;
			style.marginTop = 4;
			style.marginBottom = 2;

			VisualElement row = new()
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexWrap = Wrap.Wrap
				}
			};
			Add(row);

			_playPauseBtn = new CustomButton(() => OnPlayPause?.Invoke())
			{
				text = "Play",
				tooltip = "Space: Play/Pause playback of the clip.",
				Width = 0,
				style =
				{
					minWidth = 90,
					height = 18,
					paddingTop = 0,
					paddingBottom = 0,
					marginLeft = 0,
					marginRight = 10,
					marginTop = 0,
					marginBottom = 0
				}
			};
			row.Add(_playPauseBtn);

			if (_showLoop)
			{
				_loopToggle = new Toggle("Loop")
				{
					tooltip = "Loop playback of the clip.",
					value = true,
					style = { marginLeft = 0 }
				};

				EventCallback<ChangeEvent<bool>> loopCb = evt =>
				{
					if (_ignore) return;
					OnLoopChanged?.Invoke(evt.newValue);
				};

				_loopToggle.RegisterValueChangedCallback(loopCb);
				_callbacks.Add(() => _loopToggle.UnregisterValueChangedCallback(loopCb));

				_loopToggle.labelElement.RemoveFromClassList("unity-base-field__label");
				_loopToggle.labelElement.style.marginRight = 5;

				row.Add(_loopToggle);
			}

			VisualElement speedRow = new()
			{
				tooltip = "Adjust playback speed of the clip.",
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					marginTop = 6
				}
			};
			Add(speedRow);

			Label speedLabel = new("Speed")
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					minWidth = 44,
					opacity = 0.85f,
					marginRight = 6
				}
			};
			speedRow.Add(speedLabel);

			_speedSlider = new Slider(0f, SLIDER_MAX)
			{
				style =
				{
					flexGrow = 1f,
					marginLeft = 6,
					marginRight = 8
				}
			};

			EventCallback<ChangeEvent<float>> sliderCb = evt =>
			{
				if (_ignore) return;

				float v = Mathf.Clamp(evt.newValue, 0f, SLIDER_MAX);
				_speedField?.SetValueWithoutNotify(v);
				OnSpeedChanged?.Invoke(v);
			};

			_speedSlider.RegisterValueChangedCallback(sliderCb);
			_callbacks.Add(() => _speedSlider.UnregisterValueChangedCallback(sliderCb));
			speedRow.Add(_speedSlider);

			_speedField = new FloatField
			{
				style =
				{
					width = 64,
					marginLeft = 8,
					flexShrink = 0
				},
				formatString = "0.00"
			};

			EventCallback<ChangeEvent<float>> fieldCb = evt =>
			{
				if (_ignore) return;

				float v = Mathf.Clamp(evt.newValue, 0f, FIELD_MAX);
				_speedSlider?.SetValueWithoutNotify(Mathf.Min(v, SLIDER_MAX));
				OnSpeedChanged?.Invoke(v);
			};

			_speedField.RegisterValueChangedCallback(fieldCb);
			_callbacks.Add(() => _speedField.UnregisterValueChangedCallback(fieldCb));
			speedRow.Add(_speedField);
		}

		public void Refresh(bool playing, float speed, bool loop, Color playAccent, Color pauseAccent)
		{
			_ignore = true;

			_playPauseBtn.text = playing ? "Pause" : "Play";
			_playPauseBtn.style.backgroundColor = new StyleColor(playing ? pauseAccent : playAccent);

			float s = Mathf.Clamp(speed, 0f, FIELD_MAX);
			_speedSlider?.SetValueWithoutNotify(Mathf.Min(s, SLIDER_MAX));
			_speedField?.SetValueWithoutNotify(s);

			_loopToggle?.SetValueWithoutNotify(loop);

			_ignore = false;
		}
	}
}

