using System;
using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal static class VisualElementUtility
	{
		/// <summary>
		/// Adds a field to the parent, registers a value change callback to the host command, 
		/// and ensures the callback is unregistered when the provided scope is disposed.
		/// </summary>
		public static T CreateAndBind<T, TValue>(
			this VisualElement parent, 
			T field, 
			Action<TValue> onValueChanged, 
			CallbackScope scope) where T : VisualElement, INotifyValueChanged<TValue>
		{
			// Register callback
			EventCallback<ChangeEvent<TValue>> cb = evt => onValueChanged?.Invoke(evt.newValue);
			field.RegisterValueChangedCallback(cb);
			
			// Track cleanup
			scope?.Add(() => field.UnregisterValueChangedCallback(cb));
			
			// Add to hierarchy
			parent.Add(field);
			
			return field;
		}

		/// <summary>
		/// Simplified overload for when you don't need the field returned.
		/// </summary>
		public static void AddBound<T, TValue>(
			this VisualElement parent,
			T field,
			Action<TValue> onValueChanged,
			CallbackScope scope) where T : VisualElement, INotifyValueChanged<TValue>
		{
			parent.CreateAndBind(field, onValueChanged, scope);
		}
	}
}


