using System;
using System.Collections.Generic;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Tracks unbind actions and releases them deterministically.
	/// Designed for EditorWindow UIElements code where "rebuild" is common.
	/// </summary>
	internal sealed class CallbackScope : IDisposable
	{
		private readonly List<Action> _unbind = new();
		private bool _disposed;

		public void Add(Action unbind)
		{
			if (unbind != null)
				_unbind.Add(unbind);
		}

		public void Clear()
		{
			for (int i = _unbind.Count - 1; i >= 0; i--)
			{
				try { _unbind[i]?.Invoke(); }
				catch { /* ignored */ }
			}

			_unbind.Clear();
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;
			Clear();
		}
	}
}
