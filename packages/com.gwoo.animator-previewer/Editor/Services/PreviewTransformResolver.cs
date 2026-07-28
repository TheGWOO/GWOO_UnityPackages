using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

namespace GWOO.Editor.Tools
{
	internal sealed class PreviewTransformResolver
	{
		#region Fields

		private readonly AnimatorPreviewerState _previewerState;
		private readonly AnimatorPreviewerRuntime _previewerRuntime;

		#endregion Fields

		#region Constructors

		internal PreviewTransformResolver(PreviewContext ctx)
		{
			_previewerState = ctx.State;
			_previewerRuntime = ctx.Runtime;;
		}

		#endregion Constructors

		#region Methods

		internal void Clear()
		{
			_previewerRuntime.animatedTransforms.Clear();
			_previewerRuntime.animatedTransformIds.Clear();
			_previewerRuntime.driverTransformIds.Clear();
		}

		internal void RebuildAnimatedTransformSet(AnimatorController controller)
		{
			Clear();

			if (_previewerState.targetAnimator == null)
				return;

			Transform animatorRoot = _previewerState.targetAnimator.transform;

			AddAnimatedTransform(animatorRoot);

			if (_previewerState.mode == AnimatorPreviewerMode.Clip && _previewerState.previewClip != null)
			{
				AddDriverTransformsFromClip(animatorRoot, _previewerState.previewClip);
				AddConstraintDrivenTargets(GetConstraintSearchRoot());
				return;
			}

			if (controller != null)
			{
				AnimationClip[] clips = controller.animationClips;
				if (clips != null && clips.Length > 0)
				{
					for (int i = 0; i < clips.Length; i++)
					{
						AnimationClip clip = clips[i];
						if (clip == null)
							continue;

						AddDriverTransformsFromClip(animatorRoot, clip);
					}
				}
			}

			AddConstraintDrivenTargets(GetConstraintSearchRoot());
		}

		private void AddDriverTransformsFromClip(Transform animatorRoot, AnimationClip clip)
		{
			EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
			if (bindings == null || bindings.Length == 0)
				return;

			for (int i = 0; i < bindings.Length; i++)
			{
				EditorCurveBinding binding = bindings[i];

				if (binding.type != typeof(Transform))
					continue;

				if (!IsTransformLocalProperty(binding.propertyName))
					continue;

				Transform t = ResolvePath(animatorRoot, binding.path);
				if (t == null)
					continue;

				AddDriverTransform(t);
			}
		}

		private Transform GetConstraintSearchRoot()
		{
			if (_previewerState.targetAnimator == null)
				return null;

			GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(_previewerState.targetAnimator.gameObject);
			if (outer != null)
				return outer.transform;

			return _previewerState.targetAnimator.transform.root;
		}

		private void AddConstraintDrivenTargets(Transform searchRoot)
		{
			if (searchRoot == null)
				return;

			ParentConstraint[] parentConstraints = searchRoot.GetComponentsInChildren<ParentConstraint>(true);
			PositionConstraint[] positionConstraints = searchRoot.GetComponentsInChildren<PositionConstraint>(true);
			RotationConstraint[] rotationConstraints = searchRoot.GetComponentsInChildren<RotationConstraint>(true);
			ScaleConstraint[] scaleConstraints = searchRoot.GetComponentsInChildren<ScaleConstraint>(true);
			AimConstraint[] aimConstraints = searchRoot.GetComponentsInChildren<AimConstraint>(true);
			LookAtConstraint[] lookAtConstraints = searchRoot.GetComponentsInChildren<LookAtConstraint>(true);

			for (int iter = 0; iter < 6; iter++)
			{
				bool addedAny = false;

				addedAny |= AddConstraintTargetsFrom(parentConstraints);
				addedAny |= AddConstraintTargetsFrom(positionConstraints);
				addedAny |= AddConstraintTargetsFrom(rotationConstraints);
				addedAny |= AddConstraintTargetsFrom(scaleConstraints);
				addedAny |= AddConstraintTargetsFrom(aimConstraints);
				addedAny |= AddConstraintTargetsFrom(lookAtConstraints);

				if (!addedAny)
					break;
			}
		}

		private bool AddConstraintTargetsFrom<TConstraint>(TConstraint[] constraints)
			where TConstraint : Behaviour, IConstraint
		{
			if (constraints == null || constraints.Length == 0)
				return false;

			bool addedAny = false;

			for (int i = 0; i < constraints.Length; i++)
			{
				TConstraint c = constraints[i];
				if (c == null || !c.isActiveAndEnabled || !c.constraintActive)
					continue;

				if (!IsConstraintDrivenByPreview(c))
					continue;

				int before = _previewerRuntime.animatedTransforms.Count;

				AddAnimatedTransform(c.transform);
				_previewerRuntime.driverTransformIds.Add(c.transform.GetInstanceID());

				if (_previewerRuntime.animatedTransforms.Count != before)
					addedAny = true;
			}

			return addedAny;
		}

		private bool IsConstraintDrivenByPreview(IConstraint constraint)
		{
			int count = constraint.sourceCount;
			for (int i = 0; i < count; i++)
			{
				ConstraintSource src = constraint.GetSource(i);
				Transform srcT = src.sourceTransform;

				if (srcT == null || src.weight <= 0f)
					continue;

				if (IsSourceTransformDrivenInWorld(srcT))
					return true;
			}

			return false;
		}

		private bool IsSourceTransformDrivenInWorld(Transform src)
		{
			for (Transform t = src; t != null; t = t.parent)
			{
				if (_previewerRuntime.driverTransformIds.Contains(t.GetInstanceID()))
					return true;
			}

			return false;
		}

		private static Transform ResolvePath(Transform root, string path)
		{
			if (root == null)
				return null;

			if (string.IsNullOrEmpty(path))
				return root;

			return root.Find(path);
		}

		private static bool IsTransformLocalProperty(string propertyPath)
		{
			if (string.IsNullOrEmpty(propertyPath))
				return false;

			if (propertyPath.StartsWith("m_LocalPosition", StringComparison.Ordinal)) return true;
			if (propertyPath.StartsWith("m_LocalRotation", StringComparison.Ordinal)) return true;
			if (propertyPath.StartsWith("m_LocalScale", StringComparison.Ordinal)) return true;
			if (propertyPath.StartsWith("m_LocalEulerAnglesHint", StringComparison.Ordinal)) return true;
			if (propertyPath.StartsWith("localEulerAnglesRaw", StringComparison.Ordinal)) return true;

			return false;
		}

		private void AddDriverTransform(Transform t)
		{
			AddAnimatedTransform(t);
			_previewerRuntime.driverTransformIds.Add(t.GetInstanceID());
		}

		private void AddAnimatedTransform(Transform t)
		{
			if (t == null)
				return;

			int id = t.GetInstanceID();
			if (!_previewerRuntime.animatedTransformIds.Add(id))
				return;

			_previewerRuntime.animatedTransforms.Add(t);
		}

		#endregion Methods
	}
}

