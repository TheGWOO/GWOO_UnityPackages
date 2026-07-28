using System;
using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    [UxmlElement]
    public sealed partial class CustomButton : Button
    {
        public CustomButton() : base()
        {
            Initialize(Array.Empty<string>());
        }

        public float Width
        {
            get => GetWidth();
            set => SetWidthPercent(value);
        }

        public CustomButton(params string[] styles) : this(null, styles) {}
        public CustomButton(Action clickEvent, params string[] styles) : base(clickEvent)
        {
            Initialize(styles ?? Array.Empty<string>());
        }

        private void Initialize(string[] styles)
        {
            name = "CustomButton";
            
            RemoveFromClassList("unity-button");
            AddToClassList("custom-button");
            
            focusable = false;
            this.AddClasses(styles);
        }
        
        private float GetWidth()
        {
            return style.width.value.value; // Return the float type value of the Length
        }

        private void SetWidthPercent(float width)
        {
            style.width = width == 0 ? 
                new StyleLength(Length.Auto()) : 
                new StyleLength(Length.Percent(width));        
        }
    }
}
