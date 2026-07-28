using System;
using System.Collections.Generic;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	internal sealed class TransformPoseSnapshot
	{
		private Transform[] _transforms = Array.Empty<Transform>();
		
		private Vector3[] _positions = Array.Empty<Vector3>();
		private Quaternion[] _rotations = Array.Empty<Quaternion>();
		private Vector3[] _scales = Array.Empty<Vector3>();
		
		private int _count;

		public bool IsEmpty => _count == 0;

		public void Capture(IReadOnlyList<Transform> transforms)
		{
			if (transforms == null || transforms.Count == 0)
			{
				Clear(clearRefs: true);
				return;
			}

			int count = transforms.Count;
			EnsureCapacity(count);

			_count = count;

			for (int i = 0; i < count; i++)
			{
				Transform t = transforms[i];
				_transforms[i] = t;

				if (t == null)
					continue;

				_positions[i] = t.localPosition;
				_rotations[i] = t.localRotation;
				_scales[i] = t.localScale;
			}
		}

		public void Restore()
		{
			for (int i = 0; i < _count; i++)
			{
				Transform t = _transforms[i];
				if (t == null)
					continue;

				t.localPosition = _positions[i];
				t.localRotation = _rotations[i];
				t.localScale = _scales[i];
			}
		}

		public void Clear(bool clearRefs = false)
		{
			if (clearRefs && _count > 0)
			{
				for (int i = 0; i < _count; i++)
					_transforms[i] = null;
			}

			_count = 0;
		}

		private void EnsureCapacity(int count)
		{
			if (_transforms.Length < count)
				_transforms = new Transform[count];

			if (_positions.Length < count)
				_positions = new Vector3[count];

			if (_rotations.Length < count)
				_rotations = new Quaternion[count];

			if (_scales.Length < count)
				_scales = new Vector3[count];
		}
	}
}
