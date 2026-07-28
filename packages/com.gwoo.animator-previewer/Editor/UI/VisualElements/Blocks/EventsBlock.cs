using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Reusable block for the Previewer "Events" section.
	/// Supports Clip mode fields OR Controller mode fields.
	/// </summary>
	internal sealed class EventsBlock : VisualElement, IDisposable
	{
		private readonly bool _isClipMode;

		private bool _ignore;
		private bool _disposed;

		private Toggle _enabledToggle;
		private Toggle _drawMarkersToggle;
		private FloatField _weightThresholdField;
		private Toggle _logToggle;

		private readonly CallbackScope _callbacks = new();

		public event Action OnChanged;

		public EventsBlock(bool isClipMode)
		{
			_isClipMode = isClipMode;

			style.flexDirection = FlexDirection.Column;
			style.marginTop = 2;
			style.marginBottom = 2;

			BuildUI();
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			_callbacks.Clear();
			OnChanged = null;

			_enabledToggle = null;
			_drawMarkersToggle = null;
			_weightThresholdField = null;
			_logToggle = null;
		}

		private void BuildUI()
		{
			_enabledToggle = new Toggle("Enable Events")
			{
				tooltip = "If disabled, the Previewer won't forward AnimationEvents during preview."
			};

			EventCallback<ChangeEvent<bool>> enabledCb = _ => EmitChanged();
			_enabledToggle.RegisterValueChangedCallback(enabledCb);
			_callbacks.Add(() => _enabledToggle.UnregisterValueChangedCallback(enabledCb));
			Add(_enabledToggle);

			if (_isClipMode)
			{
				_drawMarkersToggle = new Toggle("Draw Event Markers")
				{
					tooltip = "Draw animation event markers on the timeline."
				};

				EventCallback<ChangeEvent<bool>> markersCb = _ => EmitChanged();
				_drawMarkersToggle.RegisterValueChangedCallback(markersCb);
				_callbacks.Add(() => _drawMarkersToggle.UnregisterValueChangedCallback(markersCb));
				Add(_drawMarkersToggle);
			}
			else
			{
				_weightThresholdField = new FloatField("Event Weight Threshold")
				{
					tooltip = "In Controller mode, ignore events fired by clips with weight below this threshold."
				};

				EventCallback<ChangeEvent<float>> thresholdCb = _ => EmitChanged();
				_weightThresholdField.RegisterValueChangedCallback(thresholdCb);
				_callbacks.Add(() => _weightThresholdField.UnregisterValueChangedCallback(thresholdCb));
				Add(_weightThresholdField);
			}

			_logToggle = new Toggle("Log Fired Events")
			{
				tooltip = "Logs event name/time when an event is fired by the preview."
			};

			EventCallback<ChangeEvent<bool>> logCb = _ => EmitChanged();
			_logToggle.RegisterValueChangedCallback(logCb);
			_callbacks.Add(() => _logToggle.UnregisterValueChangedCallback(logCb));
			Add(_logToggle);
		}

		private void EmitChanged()
		{
			if (_ignore) return;
			OnChanged?.Invoke();
		}

		// --- Getters used by panels ---
		public bool GetEnabledValue() => _enabledToggle != null && _enabledToggle.value;
		public bool GetDrawMarkers() => _drawMarkersToggle != null && _drawMarkersToggle.value;
		public bool GetLogValue() => _logToggle != null && _logToggle.value;

		public float GetThreshold()
		{
			if (_weightThresholdField == null)
				return 0f;

			return Mathf.Max(0f, _weightThresholdField.value);
		}

		// --- Setters used by panels (no callbacks) ---
		public void SetEnabledValue(bool v)
		{
			_ignore = true;
			_enabledToggle?.SetValueWithoutNotify(v);
			_ignore = false;
		}

		public void SetLogValue(bool v)
		{
			_ignore = true;
			_logToggle?.SetValueWithoutNotify(v);
			_ignore = false;
		}

		/// <summary>Clip-mode only.</summary>
		public void SetClipFields(bool drawMarkers)
		{
			if (_drawMarkersToggle == null)
				return;

			_ignore = true;
			_drawMarkersToggle.SetValueWithoutNotify(drawMarkers);
			_ignore = false;
		}

		/// <summary>Controller-mode only.</summary>
		public void SetControllerFields(float weightThreshold)
		{
			if (_weightThresholdField == null)
				return;

			_ignore = true;
			_weightThresholdField.SetValueWithoutNotify(Mathf.Max(0f, weightThreshold));
			_ignore = false;
		}
	}
}

