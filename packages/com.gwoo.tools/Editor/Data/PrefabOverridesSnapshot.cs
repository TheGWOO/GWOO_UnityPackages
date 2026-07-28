using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GWOO.Editor.Tools
{
	internal sealed class PrefabOverridesSnapshot
	{
		private readonly HashSet<int> _capturedSourceTransformIds = new(512);

		private readonly List<WeakReference<GameObject>> _prefabRoots = new(16);
		private readonly HashSet<int> _seenPrefabRootIds = new(16);

		private readonly List<PropertyModification> _filtered = new(256);

		public bool IsEmpty => _prefabRoots.Count == 0;

		public void Capture(IReadOnlyList<Transform> editedTransforms)
		{
			Clear();

			if (editedTransforms == null || editedTransforms.Count == 0)
				return;

			for (int i = 0; i < editedTransforms.Count; i++)
			{
				Transform t = editedTransforms[i];
				if (t == null)
					continue;

				GameObject go = t.gameObject;

				if (!PrefabUtility.IsPartOfPrefabInstance(go))
					continue;

				GameObject outerRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
				if (outerRoot != null)
				{
					int id = outerRoot.GetInstanceID();
					if (_seenPrefabRootIds.Add(id))
						_prefabRoots.Add(new WeakReference<GameObject>(outerRoot));
				}

				// PropertyModification.target refers to Prefab Asset objects (not the scene instance)
				// so cache source/original-source transform IDs.
				Object src = PrefabUtility.GetCorrespondingObjectFromSource(t);
				if (src is Transform srcT && srcT != null)
					_capturedSourceTransformIds.Add(srcT.GetInstanceID());

				Object orig = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t);
				if (orig is Transform origT && origT != null)
					_capturedSourceTransformIds.Add(origT.GetInstanceID());
			}
		}

		public void Restore()
		{
			for (int i = 0; i < _prefabRoots.Count; i++)
			{
				if (!_prefabRoots[i].TryGetTarget(out GameObject root) || root == null)
					continue;

				if (!PrefabUtility.IsPartOfPrefabInstance(root))
					continue;

				GameObject outerRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(root);
				if (outerRoot == null)
					continue;

				PropertyModification[] mods = PrefabUtility.GetPropertyModifications(outerRoot);
				if (mods == null || mods.Length == 0)
					continue;

				_filtered.Clear();

				for (int m = 0; m < mods.Length; m++)
				{
					PropertyModification mod = mods[m];

					if (mod.target is Transform targetTransform && targetTransform != null)
					{
						int targetId = targetTransform.GetInstanceID();

						if (_capturedSourceTransformIds.Contains(targetId) &&
						    IsTransformLocalProperty(mod.propertyPath))
						{
							// Drop local TRS overrides we consider "preview-driven".
							continue;
						}
					}

					_filtered.Add(mod);
				}

				PrefabUtility.SetPropertyModifications(outerRoot, _filtered.Count == 0
					? Array.Empty<PropertyModification>() : _filtered.ToArray());
			}
		}

		public void Clear()
		{
			_capturedSourceTransformIds.Clear();
			_prefabRoots.Clear();
			_seenPrefabRootIds.Clear();
			_filtered.Clear();
		}

		private static bool IsTransformLocalProperty(string propertyPath)
		{
			if (string.IsNullOrEmpty(propertyPath))
				return false;

			return propertyPath.StartsWith("m_LocalPosition", StringComparison.Ordinal)
			       || propertyPath.StartsWith("m_LocalRotation", StringComparison.Ordinal)
			       || propertyPath.StartsWith("m_LocalScale", StringComparison.Ordinal)
			       || propertyPath.StartsWith("m_LocalEulerAnglesHint", StringComparison.Ordinal);
		}
	}
}

