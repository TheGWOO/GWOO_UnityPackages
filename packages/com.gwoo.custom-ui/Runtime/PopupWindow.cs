using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public class PopupWindow : VisualElement
    {
        private VisualElement _contentContainer;
        public VisualElement ContentContainer => _contentContainer;
        
        public PopupWindow()
        {
            AddToClassList("custom-popup-window");

            pickingMode = PickingMode.Position;

            _contentContainer = new();
            _contentContainer.AddToClassList("custom-popup-window__container");
            Add(ContentContainer);

            RegisterCallback<PointerDownEvent>(OnPointerDownOutside);

            ContentContainer.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        }

        private void OnPointerDownOutside(PointerDownEvent evt)
        {
            if (evt.target == this)
            {
                Close();
            }
        }

        protected void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
