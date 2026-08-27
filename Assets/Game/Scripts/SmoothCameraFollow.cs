using UnityEngine;

namespace TinyRpg
{
    /// Camara con seguimiento suave: una zona muerta hace que tarde un instante en
    /// reaccionar cuando el jugador (o su dash) se mueve, y luego lo alcanza con
    /// suavizado. Incluye temblor de pantalla acumulativo para los impactos.
    public class SmoothCameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.28f;
        public float deadzoneRadius = 0.65f;
        public float recenterSpeed = 2.4f;
        public Vector2 boundsMin = new Vector2(0f, 0f);
        public Vector2 boundsMax = new Vector2(96f, 64f);
        public float shakeMaxOffset = 0.28f;
        public float shakeDecay = 2.2f;

        static SmoothCameraFollow instance;

        Camera cam;
        Vector2 anchor;      // punto que la camara persigue (arrastrado por la zona muerta)
        Vector2 velocity;
        float trauma;

        void Awake()
        {
            instance = this;
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (target != null)
            {
                anchor = target.position;
                SnapTo(anchor);
            }
        }

        public static void Shake(float amount)
        {
            if (!GameSettings.ScreenShake) return; // desactivable en Configuracion
            if (instance != null)
                instance.trauma = Mathf.Clamp01(instance.trauma + amount);
        }

        void LateUpdate()
        {
            if (target == null) return;

            // La zona muerta: el ancla solo se arrastra cuando el objetivo se aleja de
            // ella, asi la camara "tarda un momento" en empezar a seguir. Cuando el
            // objetivo queda dentro (se detuvo), el ancla termina de alcanzarlo para
            // que la camara acabe centrando al jugador.
            Vector2 targetPos = target.position;
            Vector2 delta = targetPos - anchor;
            if (delta.magnitude > deadzoneRadius)
                anchor = targetPos - delta.normalized * deadzoneRadius;
            else
                anchor = Vector2.MoveTowards(anchor, targetPos, recenterSpeed * Time.deltaTime);

            Vector2 current = new Vector2(transform.position.x, transform.position.y);
            Vector2 next = Vector2.SmoothDamp(current, anchor, ref velocity, smoothTime);
            next = ClampToBounds(next);

            // Temblor de pantalla (decae solo).
            if (trauma > 0f)
            {
                trauma = Mathf.Max(0f, trauma - shakeDecay * Time.deltaTime);
                float shake = trauma * trauma;
                float t = Time.time * 34f;
                next += new Vector2(
                    (Mathf.PerlinNoise(t, 1.7f) - 0.5f) * 2f,
                    (Mathf.PerlinNoise(3.9f, t) - 0.5f) * 2f) * (shakeMaxOffset * shake);
            }

            transform.position = new Vector3(next.x, next.y, -10f);
        }

        void SnapTo(Vector2 pos)
        {
            Vector2 p = ClampToBounds(pos);
            transform.position = new Vector3(p.x, p.y, -10f);
        }

        /// Encuadre inmediato sobre el objetivo (al cambiar de mapa).
        public void SnapToTarget()
        {
            if (target == null) return;
            anchor = target.position;
            velocity = Vector2.zero;
            SnapTo(anchor);
        }

        /// Llegada a un mapa nuevo. La posicion SI salta (el mapa anterior ya no
        /// existe, un barrido no tendria sentido), pero el encuadre se abre y se
        /// cierra suavemente para que no sea un corte seco.
        public void ArriveAtTarget(float zoomOut = 1.22f, float duration = 0.55f)
        {
            SnapToTarget();
            if (cam == null || !cam.orthographic) return;
            if (arrival != null) StopCoroutine(arrival);
            arrival = StartCoroutine(ArrivalZoom(zoomOut, duration));
        }

        Coroutine arrival;
        float baseSize = -1f;

        System.Collections.IEnumerator ArrivalZoom(float zoomOut, float duration)
        {
            // El tamano base se guarda una sola vez: si se encadenan llegadas,
            // no se acumula el zoom.
            if (baseSize <= 0f) baseSize = cam.orthographicSize;
            float from = baseSize * zoomOut;
            float t = 0f;

            cam.orthographicSize = from;
            while (t < duration)
            {
                // Tiempo sin escalar: la llegada tambien se ve con el juego
                // pausado en la seleccion de clase.
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                k = 1f - (1f - k) * (1f - k); // ease-out
                cam.orthographicSize = Mathf.Lerp(from, baseSize, k);
                yield return null;
            }
            cam.orthographicSize = baseSize;
            arrival = null;
        }

        Vector2 ClampToBounds(Vector2 pos)
        {
            if (cam == null || !cam.orthographic) return pos;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float minX = boundsMin.x + halfW, maxX = boundsMax.x - halfW;
            float minY = boundsMin.y + halfH, maxY = boundsMax.y - halfH;
            pos.x = minX <= maxX ? Mathf.Clamp(pos.x, minX, maxX) : (boundsMin.x + boundsMax.x) * 0.5f;
            pos.y = minY <= maxY ? Mathf.Clamp(pos.y, minY, maxY) : (boundsMin.y + boundsMax.y) * 0.5f;
            return pos;
        }
    }
}
