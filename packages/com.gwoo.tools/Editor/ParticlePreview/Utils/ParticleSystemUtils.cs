using UnityEngine;

namespace GWOO.Editor.ParticlePreview
{
	/// <summary>
	/// Small, testable helpers for ParticleSystem operations.
	/// </summary>
	internal static class ParticleSystemUtils
	{
		public static bool IsDefinitelyStopped(ParticleSystem ps)
		{
			if (!ps) return true;

			try { if (ps.IsAlive(true)) return false; } catch { }
			try { if (ps.particleCount > 0) return false; } catch { }
			try { if (ps.isPlaying || ps.isEmitting) return false; } catch { }

			return true;
		}

		public static void StopAndClear(ParticleSystem ps, bool includeChildren)
		{
			if (!ps) return;

			try { ps.Stop(includeChildren, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
			try { ps.Clear(includeChildren); } catch { }
			try { ps.time = 0f; } catch { }
			try { ps.Clear(includeChildren); } catch { }
		}

		public static uint MixSeed(uint baseSeed, int instanceId)
		{
			unchecked
			{
				uint x = baseSeed;
				x ^= (uint)instanceId;
				x *= 16777619u;
				x ^= x >> 13;
				x *= 1274126177u;
				x ^= x >> 16;
				return x;
			}
		}

		public static bool TryGetSeed(ParticleSystem ps, out uint seed)
		{
			seed = 0;
			if (!ps) return false;

			try { seed = ps.randomSeed; return true; }
			catch { return false; }
		}

		public static uint SafeGetSeed(ParticleSystem ps) => TryGetSeed(ps, out uint s) ? s : 0;

		public static bool TryGetUseAuto(ParticleSystem ps, out bool useAuto)
		{
			useAuto = false;
			if (!ps) return false;

			try { useAuto = ps.useAutoRandomSeed; return true; }
			catch { return false; }
		}

		public static bool SafeGetUseAuto(ParticleSystem ps) => TryGetUseAuto(ps, out bool a) && a;
	}
}
