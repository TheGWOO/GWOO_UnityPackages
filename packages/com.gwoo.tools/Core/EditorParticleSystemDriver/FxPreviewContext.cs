using System;

namespace GWOO.Editor.ParticlePreview
{
	public static class FxPreviewContext
	{
		public static bool Active { get; private set; }

		/// <summary>Seconds in clip space (event time being dispatched).</summary>
		public static float ClipEventTime { get; private set; }

		/// <summary>
		/// Changes when preview context changes (clip/mode switch, etc.).
		/// Used to flush deterministic preview state cleanly.
		/// </summary>
		public static int ContextKey { get; private set; }

		/// <summary>Changes per bind/rebind session (per tool preview run).</summary>
		public static int SessionKey { get; private set; }

		public readonly struct Scope : IDisposable
		{
			private readonly bool _prevActive;
			private readonly float _prevTime;
			private readonly int _prevContextKey;
			private readonly int _prevSessionKey;

			public Scope(float clipEventTime, int contextKey, int sessionKey)
			{
				_prevActive = Active;
				_prevTime = ClipEventTime;
				_prevContextKey = ContextKey;
				_prevSessionKey = SessionKey;

				Active = true;
				ClipEventTime = clipEventTime;
				ContextKey = contextKey;
				SessionKey = sessionKey;
			}

			public void Dispose()
			{
				Active = _prevActive;
				ClipEventTime = _prevTime;
				ContextKey = _prevContextKey;
				SessionKey = _prevSessionKey;
			}
		}
	}
}
