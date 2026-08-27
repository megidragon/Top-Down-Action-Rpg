using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Boton tactil de accion. Los marcados como 'aimable' ademas apuntan:
    /// manten pulsado y arrastra para elegir direccion y distancia; suelta para
    /// ejecutar. Un toque seco (sin arrastre) deja que GameInput auto-apunte.
    public class TouchActionButton : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public TouchControls owner;
        public TouchAction action;
        public bool aimable;

        Graphic graphic;
        Color baseColor;
        Vector2 pressPos;

        void Awake()
        {
            graphic = GetComponent<Graphic>();
            if (graphic != null) baseColor = graphic.color;
        }

        public void OnPointerDown(PointerEventData e)
        {
            pressPos = e.position;
            Tint(0.55f);
            if (owner != null) owner.ActionDown(action, aimable);
        }

        public void OnDrag(PointerEventData e)
        {
            if (aimable && owner != null) owner.ActionDrag(e.position - pressPos);
        }

        public void OnPointerUp(PointerEventData e)
        {
            Tint(1f);
            if (owner != null) owner.ActionUp(action, aimable);
        }

        void Tint(float scale)
        {
            if (graphic == null) return;
            graphic.color = new Color(baseColor.r * scale, baseColor.g * scale,
                baseColor.b * scale, baseColor.a);
        }
    }
}
