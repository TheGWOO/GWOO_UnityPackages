using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.UIElements
{
    public static class UIElementExtensions
    {
        public static void AddClasses(this VisualElement element, params string[] styles)
        {
            foreach (string selector in styles)
            {
                element.AddToClassList(selector);
            }
        }
        
        public static VisualElement FindChildByStyle(VisualElement parent, string ussClass)
        {
            Stack<VisualElement> stack = new();
            stack.Push(parent);

            while (stack.Count > 0)
            {
                VisualElement current = stack.Pop();
                if (current.ClassListContains(ussClass))
                {
                    return current;
                }

                foreach (VisualElement child in current.Children())
                {
                    stack.Push(child);
                }
            }

            return null;
        }
    
        public static void SetDisplay(this VisualElement element, bool displayed)
        {
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        public static void ForceUpdate(this ScrollView view)
        {
            view.schedule.Execute(() =>
            {
                Rect fakeOldRect = Rect.zero;
                Rect fakeNewRect = view.layout;
       
                using GeometryChangedEvent evt = GeometryChangedEvent.GetPooled(fakeOldRect, fakeNewRect);
                evt.target = view.contentContainer;
                view.contentContainer.SendEvent(evt);
            });
        }
    }
}