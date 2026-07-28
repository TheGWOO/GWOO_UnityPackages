using System;

namespace GWOO.Editor.Tools
{
	public struct StateKey : IEquatable<StateKey>
	{
		public int layer;
		public int stateHash;

		public bool Equals(StateKey other) => layer == other.layer && stateHash == other.stateHash;
		public override int GetHashCode() => HashCode.Combine(layer, stateHash);
	}
}
