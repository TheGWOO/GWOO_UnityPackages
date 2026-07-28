using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public class ToggledFoldout : CustomFoldout
    {
        private CustomToggle _toggle = new();
        public CustomToggle Toggle => _toggle;

        public bool toggleValue
        {
            get => _toggle.value;
            set => _toggle.value = value;
        }

        public ToggledFoldout(bool addSeparator = true, params string[] style) : base(addSeparator, style)
        {
            name = "ToggledFoldout";
            
            TextPrefix = "";
            _toggle.value = toggleValue;
            _toggle.style.marginRight = new StyleLength(new Length(12));
            
            _toggle.AddClasses(style);

            FoldoutHeader.Insert(1, _toggle);
        }

        public void RegisterToggleCallback(EventCallback<ChangeEvent<bool>> callback)
        {
            _toggle.RegisterValueChangedCallback(callback);
        }
    }
}