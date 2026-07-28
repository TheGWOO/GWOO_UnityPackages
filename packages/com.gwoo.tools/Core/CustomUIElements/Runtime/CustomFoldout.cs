using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public class CustomFoldout : VisualElement
    {
        protected Foldout Foldout;
        protected VisualElement FoldoutHeader;

        protected string TextPrefix = "--- ";

        public string text
        {
            get => Foldout.text;
            set => Foldout.text = TextPrefix + value;
        }

        public bool foldoutValue
        {
            get => Foldout.value;
            set => Foldout.value = value;
        }

        public CustomFoldout(bool addSeparator = true, params string[] style)
        {
            name = "CustomFoldout";
            
            this.style.borderBottomLeftRadius = 10;
            this.style.borderBottomRightRadius = 10;
            this.style.borderTopLeftRadius = 10;
            this.style.borderTopRightRadius = 10;
            
            Foldout = new Foldout
            {
                text = "--- CUSTOM FOLDOUT",
                value = true,
            };

            FoldoutHeader = Foldout.Q<VisualElement>(className: "unity-toggle__input");

            FoldoutHeader.AddClasses(style);

            if (addSeparator)
            {
                Separator separator = new() { style = { marginTop = 10 } };
                Add(separator);
            }

            Add(Foldout);
        }

        public void AddContent(VisualElement content)
        {
            Foldout.contentContainer.Add(content);
        }
        
        public void RegisterFoldoutValueCallback(EventCallback<ChangeEvent<bool>> callback)
        {
            Foldout.RegisterValueChangedCallback(callback);
        }
    }
}