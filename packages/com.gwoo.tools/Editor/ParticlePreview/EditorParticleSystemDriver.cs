using UnityEditor;
using UnityEngine;

namespace GWOO.Editor.ParticlePreview
{
	/// <summary>
	/// Deterministic ParticleSystem preview driver for Edit Mode.
	///
	/// Facade:
	/// - Exposes the stable public API used by your tooling.
	/// - Delegates all behavior/state to ParticlePreviewSession.
	/// - Hooks are registered from a dedicated hooks class.
	/// </summary>
	[InitializeOnLoad]
	public static class EditorParticleSystemDriver
	{
		public struct Settings
		{
			public bool includeChildren;

			public bool deterministicSeed;
			public uint seed;

			public bool fixedTimeStep;

			public bool manualBurstOnReset;
			public int burstCount;

			public static Settings Default => new()
			{
				includeChildren = true,
				deterministicSeed = true,
				seed = 1u,
				fixedTimeStep = true,
				manualBurstOnReset = false,
				burstCount = 1
			};

			public bool Equals(in Settings other)
			{
				return includeChildren == other.includeChildren &&
				       deterministicSeed == other.deterministicSeed &&
				       seed == other.seed &&
				       fixedTimeStep == other.fixedTimeStep &&
				       manualBurstOnReset == other.manualBurstOnReset &&
				       burstCount == other.burstCount;
			}
		}

		private static readonly ParticlePreviewSession SESSION = new();

		static EditorParticleSystemDriver()
		{
			EditorParticleSystemDriverHooks.Register(SESSION);
		}

		// --------------------
		// Public API (Session)
		// --------------------

		public static void BeginSession(int sessionKey) => SESSION.BeginSession(sessionKey);

		public static void EndSession(bool clearParticles = true) => SESSION.EndSession(clearParticles);

		// --------------------
		// Public API (Context)
		// --------------------

		public static void SetContextKey(int contextKey, bool clearParticlesOnExit = true)
			=> SESSION.SetContextKey(contextKey, clearParticlesOnExit);

		// --------------------
		// Public API (Runtime)
		// --------------------

		public static void RegisterOrUpdate(
			ParticleSystem ps,
			float originAbsTime,
			bool restartNow,
			in Settings settings,
			int contextKey)
		{
			SESSION.RegisterOrUpdate(ps, originAbsTime, restartNow, settings, contextKey);
		}

		public static void Unregister(ParticleSystem ps, bool clearNow) => SESSION.Unregister(ps, clearNow);

		public static void Advance(float dtAbs) => SESSION.Advance(dtAbs);

		public static void Seek(float absoluteTime) => SESSION.Seek(absoluteTime);

		// --------------------
		// Internal (save hooks)
		// --------------------

		internal static void NotifyWillSaveAssets(string[] paths) => SESSION.NotifyWillSaveAssets(paths);
	}
}
