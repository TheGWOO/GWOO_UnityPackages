using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public class Separator : VisualElement
    {
        public Separator(params string[] styles) : this(2, styles) {}
        public Separator(int height = 2, params string[] styles)       
        {
            name = "Separator";
            
            this.AddClasses(styles);

            style.height = new StyleLength(height);
        }
        
        public void SetMarginSpace(float value = 10f)
        {
            style.marginTop = new StyleLength(value);
            style.marginBottom = new StyleLength(value);
        }
    }
}