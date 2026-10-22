using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Escape4Now.UI
{
    public static class UiPointer
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        public static bool IsOverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            Hits.Clear();
            EventSystem.current.RaycastAll(pointer, Hits);
            foreach (var hit in Hits)
                if (hit.module is UnityEngine.UI.GraphicRaycaster) return true;
            return false;
        }
    }
}
