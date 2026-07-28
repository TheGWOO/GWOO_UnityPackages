using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
	internal sealed class FloatBinder : IParamBinder
	{
		private readonly int _hash;
		private readonly Slider _slider;

		public FloatBinder(int hash, Slider slider)
		{
			_hash = hash;
			_slider = slider;
		}

		public void Refresh(IAnimatorPreviewerHost host)
		{
			if (host.TryGetFloat(_hash, out float v))
			{
				_slider.SetValueWithoutNotify(v);
			}
		}
	}
}
