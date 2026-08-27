using UnityEngine;
using UnityEngine.EventSystems;

namespace TinyRpg
{
    /// Zona del stick de movimiento (mitad inferior izquierda). Es un joystick
    /// FLOTANTE: la base aparece donde pones el dedo, asi no hay que buscar un
    /// mando fijo en pantalla.
    public class TouchStickZone : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public TouchControls owner;
        public RectTransform stickBase;
        public RectTransform stickKnob;
        public float radius = 130f;

        RectTransform canvasRect;
        int activePointer = -1;

        void Awake()
        {
            canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        }

        void Start()
        {
            // OJO: no ocultar en Awake. AddComponent ejecuta Awake al instante,
            // ANTES de que TouchControls asigne stickBase/stickKnob, y el stick
            // se quedaba visible en mitad de la pantalla.
            Show(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (activePointer != -1) return; // ya hay un dedo moviendo
            activePointer = e.pointerId;
            if (ToCanvas(e.position, out Vector2 local))
            {
                stickBase.anchoredPosition = local;
                stickKnob.anchoredPosition = local;
            }
            Show(true);
            owner?.SetMove(Vector2.zero);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != activePointer) return;
            if (!ToCanvas(e.position, out Vector2 local)) return;

            Vector2 delta = local - stickBase.anchoredPosition;
            float len = delta.magnitude;
            Vector2 clamped = len > radius ? delta / len * radius : delta;
            stickKnob.anchoredPosition = stickBase.anchoredPosition + clamped;

            // Zona muerta pequena para que un toque no derive en movimiento.
            Vector2 move = clamped / radius;
            if (move.magnitude < 0.15f) move = Vector2.zero;
            else if (move.sqrMagnitude > 1f) move.Normalize();
            owner?.SetMove(move);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != activePointer) return;
            activePointer = -1;
            Show(false);
            owner?.SetMove(Vector2.zero);
        }

        bool ToCanvas(Vector2 screenPos, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, null, out local);
        }

        void Show(bool visible)
        {
            if (stickBase != null) stickBase.gameObject.SetActive(visible);
            if (stickKnob != null) stickKnob.gameObject.SetActive(visible);
        }
    }
}
