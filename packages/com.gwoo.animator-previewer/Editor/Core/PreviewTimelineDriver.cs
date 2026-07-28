using GWOO.Editor.ParticlePreview;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	/// <summary>
	/// Centralized timeline driver for preview systems (particles, etc.).
	/// AnimatorPreviewer feeds it clip-absolute time or dt; the particle driver uses that.
	/// </summary>
	internal static class PreviewTimelineDriver
	{
		internal static float AbsoluteTime { get; private set; }

		internal static void SeekAbsolute(float clipAbsTime)
		{
			if (Application.isPlaying)
				return;

			AbsoluteTime = Mathf.Max(0f, clipAbsTime);
			EditorParticleSystemDriver.Seek(AbsoluteTime);
		}

		internal static void Advance(float dt)
		{
			if (Application.isPlaying)
				return;

			if (dt <= 0f)
				return;

			AbsoluteTime += dt;
			EditorParticleSystemDriver.Advance(dt);
		}

		internal static void Reset()
		{
			SeekAbsolute(0f);
		}
	}
}

