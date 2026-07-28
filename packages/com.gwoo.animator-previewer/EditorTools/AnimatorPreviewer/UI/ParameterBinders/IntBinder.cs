using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class IntBinder : IParamBinder
	{
		private readonly int _hash;
		private readonly IntegerField _field;

		public IntBinder(int hash, IntegerField field)
		{
			_hash = hash;
			_field = field;
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			if (host.TryGetInt(_hash, out int v))
			{
				_field.SetValueWithoutNotify(v);
			}
		}
	}
}
