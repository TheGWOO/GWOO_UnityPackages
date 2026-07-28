using System;
using UnityEditor;

namespace GWOO.Editor.Tools
{
	internal sealed class DelayedAction
	{
		private readonly Action _action;
		private bool _queued;

		internal bool IsQueued => _queued;
		
		internal DelayedAction(Action action)
		{
			_action = action;
		}

		internal void Queue()
		{
			if (_queued) return;
			_queued = true;
			EditorApplication.delayCall += Invoke;
		}

		internal void Cancel()
		{
			if (!_queued) return;
			_queued = false;
			EditorApplication.delayCall -= Invoke;
		}

		private void Invoke()
		{
			EditorApplication.delayCall -= Invoke;
			_queued = false;
			_action?.Invoke();
		}
	}
}
