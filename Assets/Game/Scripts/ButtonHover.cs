using UnityEngine;
using UnityEngine.EventSystems;

namespace TinyRpg
{
    /// Pequeno efecto de hover para botones: crece suavemente al pasar el raton.
    /// Usa tiempo sin escalar para funcionar tambien con el juego en pausa.
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float hoverScale = 1.07f;
        public float speed = 12f;

        float target = 1f;
        Vector3 baseScale;

        void Awake()
        {
            baseScale = transform.localScale;
        }

        void OnEnable()
        {
            target = 1f;
            transform.localScale = baseScale;
        }

        public void OnPointerEnter(PointerEventData eventData) => target = hoverScale;
        public void OnPointerExit(PointerEventData eventData) => target = 1f;

        void Update()
        {
            float current = transform.localScale.x / baseScale.x;
            float next = Mathf.Lerp(current, target, Time.unscaledDeltaTime * speed);
            transform.localScale = baseScale * next;
        }
    }
}
