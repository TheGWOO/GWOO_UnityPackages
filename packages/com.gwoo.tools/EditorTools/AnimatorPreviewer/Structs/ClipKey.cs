using System;

namespace GWOO.Editor.Tools
{
	public struct ClipKey : IEquatable<ClipKey>
	{
		public int layer;
		public int clipId;

		public bool Equals(ClipKey other) => layer == other.layer && clipId == other.clipId;
		public override int GetHashCode() => HashCode.Combine(layer, clipId);
	}
}
