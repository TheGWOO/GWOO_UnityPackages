using System;
using GWOO.Editor.ParticlePreview;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Bridges the AnimatorPreviewer with the EditorParticleSystemDriver to allow particle scrubbing.
	/// </summary>
	internal sealed class PreviewFxBridge
	{
		private static int _sessionCounter;

		private readonly AnimatorPreviewerState _previewerState;

		private int _sessionId;
		private int _contextGen;
		private int _lastContextKey;

		internal int SessionId => _sessionId;
		internal int LastContextKey => _lastContextKey;

		internal PreviewFxBridge(AnimatorPreviewerState previewerState)
		{
			_previewerState = previewerState;
		}

		internal void BeginSessionIfNeeded()
		{
			if (_sessionId != 0)
				return;

			_sessionId = unchecked(++_sessionCounter);
			_contextGen = 0;
			_lastContextKey = 0;

			EditorParticleSystemDriver.BeginSession(_sessionId);
		}

		internal void EndSession()
		{
			if (_sessionId == 0)
				return;

			EditorParticleSystemDriver.EndSession();

			_sessionId = 0;
			_contextGen = 0;
			_lastContextKey = 0;
		}

		internal void BumpContext() => _contextGen = unchecked(_contextGen + 1);

		internal void SyncContext(bool force)
		{
			if (_sessionId == 0)
				return;

			int animatorId = _previewerState.targetAnimator != null ? _previewerState.targetAnimator.GetInstanceID() : 0;
			int modeId = (int)_previewerState.mode;
			int clipId = (_previewerState.mode == AnimatorPreviewerMode.Clip && _previewerState.previewClip != null) ? _previewerState.previewClip.GetInstanceID() : 0;

			int key = HashCode.Combine(_sessionId, _contextGen, animatorId, modeId, clipId);

			if (!force && key == _lastContextKey)
				return;

			_lastContextKey = key;
			EditorParticleSystemDriver.SetContextKey(key);
		}
	}
}

