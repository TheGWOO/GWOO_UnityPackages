using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class BoolBinder : IParamBinder
	{
		private readonly int _hash;
		private readonly Toggle _toggle;

		public BoolBinder(int hash, Toggle toggle)
		{
			_hash = hash;
			_toggle = toggle;
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			if (host.TryGetBool(_hash, out bool v))
			{
				_toggle.SetValueWithoutNotify(v);
			}
		}
	}
}
