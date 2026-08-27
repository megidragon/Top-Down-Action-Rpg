using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Camara libre para observar las pruebas del coliseo. Mientras esta
    /// activa el seguimiento del jugador queda desconectado y el raton y el
    /// teclado mueven el punto de vista.
    ///
    ///  WASD / flechas  desplazar
    ///  Rueda           acercar y alejar
    ///  Arrastrar con boton derecho o central  desplazar
    ///
    /// Usa tiempo SIN escalar porque las pruebas corren a 8x o mas: si no, la
    /// camara volaria a esa misma velocidad.
    [RequireComponent(typeof(Camera))]
    public class FreeCameraController : MonoBehaviour
    {
        public float panSpeed = 14f;
        public float zoomSpeed = 8f;
        public float minSize = 4f;
        // Suficiente para abarcar la rejilla entera de arenas de entrenamiento.
        public float maxSize = 220f;

        public bool Active { get; private set; }

        Camera cam;
        SmoothCameraFollow follow;
        float restoreSize = 7f;
        Vector2 dragOrigin;
        bool dragging;

        void Awake()
        {
            cam = GetComponent<Camera>();
            follow = GetComponent<SmoothCameraFollow>();
        }

        /// Toma el control y encuadra 'center' con el zoom pedido.
        public void Activate(Vector2 center, float size)
        {
            if (!Active)
            {
                restoreSize = cam.orthographicSize;
                Active = true;
            }
            if (follow != null) follow.enabled = false;
            cam.orthographicSize = Mathf.Clamp(size, minSize, maxSize);
            transform.position = new Vector3(center.x, center.y, -10f);
        }

        /// Devuelve la camara al jugador.
        public void Deactivate()
        {
            if (!Active) return;
            Active = false;
            cam.orthographicSize = restoreSize;
            if (follow != null)
            {
                follow.enabled = true;
                follow.SnapToTarget();
            }
        }

        void Update()
        {
            if (!Active) return;

            float dt = Time.unscaledDeltaTime;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // El zoom escala tambien la velocidad: alejado se recorre mas
            // terreno por pulsacion, que es lo que uno espera.
            float speed = panSpeed * (cam.orthographicSize / 10f);

            if (keyboard != null)
            {
                Vector2 move = Vector2.zero;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (move.sqrMagnitude > 1f) move.Normalize();
                if (move.sqrMagnitude > 0f)
                    transform.position += (Vector3)(move * speed * dt);
            }

            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float step = Mathf.Sign(scroll) * zoomSpeed * (cam.orthographicSize / 12f);
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - step, minSize, maxSize);
            }

            bool held = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            Vector2 screen = mouse.position.ReadValue();
            if (held && !dragging)
            {
                dragging = true;
                dragOrigin = cam.ScreenToWorldPoint(screen);
            }
            else if (held)
            {
                // Arrastre "agarrando el mundo": el punto bajo el cursor se
                // queda bajo el cursor.
                Vector2 now = cam.ScreenToWorldPoint(screen);
                Vector2 delta = dragOrigin - now;
                transform.position += new Vector3(delta.x, delta.y, 0f);
            }
            else
            {
                dragging = false;
            }
        }
    }
}
