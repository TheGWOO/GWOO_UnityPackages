using System;
using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    [UxmlElement]
    public partial class CustomToggle : Toggle
    {
        public CustomToggle() : this(Array.Empty<string>())
        {
        }

        public CustomToggle(params string[] style)
        {
            name = "CustomToggle";
            
            RemoveFromClassList("unity-toggle");
            this.AddClasses(style ?? Array.Empty<string>());
        }
    }
}
