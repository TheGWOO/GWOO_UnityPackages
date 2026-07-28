using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GWOO.Editor.ParticlePreview
{
	/// <summary>
	/// Seed-only prefab override hygiene:
	/// - capture seed-related PropertyModifications at baseline
	/// - restore them later without touching unrelated overrides
	/// </summary>
	internal static class PrefabOverridesUtils
	{
		public static bool IsScenePrefabInstance(Object obj)
		{
			if (!obj) return false;
			return PrefabUtility.IsPartOfPrefabInstance(obj) && !PrefabUtility.IsPartOfPrefabAsset(obj);
		}

		public static PropertyModification[] ExtractSeedPrefabMods(PropertyModification[] mods)
		{
			if (mods == null || mods.Length == 0)
				return null;

			List<PropertyModification> seed = null;

			for (int i = 0; i < mods.Length; i++)
			{
				PropertyModification m = mods[i];
				if (m == null) continue;

				if (IsSeedPropertyPath(m.propertyPath))
				{
					seed ??= new List<PropertyModification>(4);
					seed.Add(CloneMod(m));
				}
			}

			return seed != null && seed.Count > 0 ? seed.ToArray() : null;
		}

		public static void RestoreSeedPrefabModsToBaseline(ParticleSystem ps, PropertyModification[] baselineSeedMods)
		{
			if (!ps) return;
			if (!IsScenePrefabInstance(ps)) return;

			PropertyModification[] current;
			try { current = PrefabUtility.GetPropertyModifications(ps); }
			catch { return; }

			List<PropertyModification> keep = null;

			if (current != null && current.Length > 0)
			{
				keep = new List<PropertyModification>(current.Length);

				for (int i = 0; i < current.Length; i++)
				{
					PropertyModification m = current[i];
					if (m == null) continue;

					if (!IsSeedPropertyPath(m.propertyPath))
						keep.Add(m);
				}
			}

			if (baselineSeedMods != null && baselineSeedMods.Length > 0)
			{
				keep ??= new List<PropertyModification>(baselineSeedMods.Length + 4);

				for (int i = 0; i < baselineSeedMods.Length; i++)
				{
					PropertyModification m = baselineSeedMods[i];
					if (m == null) continue;
					keep.Add(CloneMod(m));
				}
			}

			try
			{
				PrefabUtility.SetPropertyModifications(ps, keep != null && keep.Count > 0 ? keep.ToArray() : null);
			}
			catch { }
		}

		private static bool IsSeedPropertyPath(string propertyPath)
		{
			if (string.IsNullOrEmpty(propertyPath))
				return false;

			return propertyPath.IndexOf("randomSeed", StringComparison.OrdinalIgnoreCase) >= 0 ||
			       propertyPath.IndexOf("autoRandomSeed", StringComparison.OrdinalIgnoreCase) >= 0 ||
			       propertyPath.IndexOf("useAutoRandomSeed", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static PropertyModification CloneMod(PropertyModification m)
		{
			return new PropertyModification
			{
				target = m.target,
				propertyPath = m.propertyPath,
				value = m.value,
				objectReference = m.objectReference
			};
		}
	}
}
