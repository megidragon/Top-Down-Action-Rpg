using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Capa unica de entrada del jugador: teclado+raton en escritorio y
    /// controles tactiles en movil. El resto del juego (PlayerController,
    /// fogata, mercaderes...) pregunta AQUI, nunca al dispositivo.
    ///
    /// Los "edges" (pulsaciones) se consumen al leerlos: cada pulsacion la
    /// atiende un unico lector y no se pierde aunque el EventSystem procese
    /// el toque despues del Update del jugador.
    public static class GameInput
    {
        /// Controles tactiles activos (los registra TouchControls al habilitarse).
        public static TouchControls Touch;

        public static bool TouchMode => Touch != null && Touch.Active;

        /// Alcance maximo al que apunta un arrastre completo. Cada clase recorta
        /// despues segun su propio alcance (arquero 11, mago 7...).
        public const float MaxAimRange = 11f;
        const float AutoAimRange = 12f;

        // ---------------- Movimiento ----------------

        public static Vector2 Move
        {
            get
            {
                if (TouchMode) return Touch.MoveInput;
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                Vector2 move = Vector2.zero;
                if (k.wKey.isPressed) move.y += 1f;
                if (k.sKey.isPressed) move.y -= 1f;
                if (k.dKey.isPressed) move.x += 1f;
                if (k.aKey.isPressed) move.x -= 1f;
                if (move.sqrMagnitude > 1f) move.Normalize();
                return move;
            }
        }

        // ---------------- Acciones ----------------

        public static bool DashPressed => TouchMode
            ? Touch.ConsumeDash()
            : Key(k => k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame);

        public static bool PrimaryPressed => TouchMode
            ? Touch.ConsumePrimaryDown()
            : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public static bool PrimaryReleased => TouchMode
            ? Touch.ConsumePrimaryUp()
            : Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        public static bool SecondaryPressed => TouchMode
            ? Touch.ConsumeSecondaryDown()
            : Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        public static bool SpecialPressed => TouchMode
            ? Touch.ConsumeSpecial()
            : Key(k => k.spaceKey.wasPressedThisFrame);

        public static bool InteractPressed => TouchMode
            ? Touch.ConsumeInteract()
            : Key(k => k.eKey.wasPressedThisFrame);

        public static bool AllyAttackPressed => TouchMode
            ? Touch.ConsumeAllyAttack()
            : Key(k => k.cKey.wasPressedThisFrame);

        public static bool AllyFleePressed => TouchMode
            ? Touch.ConsumeAllyFlee()
            : Key(k => k.vKey.wasPressedThisFrame);

        /// Slot de inventario 0-3 (teclas 1-4, o toque sobre el hueco del HUD).
        public static bool ItemPressed(int slot)
        {
            if (TouchMode) return Touch.ConsumeItem(slot);
            var k = Keyboard.current;
            if (k == null) return false;
            switch (slot)
            {
                case 0: return k.digit1Key.wasPressedThisFrame;
                case 1: return k.digit2Key.wasPressedThisFrame;
                case 2: return k.digit3Key.wasPressedThisFrame;
                case 3: return k.digit4Key.wasPressedThisFrame;
                default: return false;
            }
        }

        static bool Key(System.Func<Keyboard, bool> test)
        {
            var k = Keyboard.current;
            return k != null && test(k);
        }

        // ---------------- Apuntado ----------------

        /// Direccion y punto de apuntado para un personaje situado en 'origin'.
        ///  - Raton: hacia el cursor.
        ///  - Tactil arrastrando un boton de accion: hacia el arrastre, con la
        ///    distancia proporcional a cuanto se estira.
        ///  - Tactil sin arrastrar (toque seco): auto-apuntado al enemigo vivo
        ///    mas cercano; si no hay ninguno, se mantiene 'fallbackDir'.
        public static void ResolveAim(Vector2 origin, Vector2 fallbackDir,
            out Vector2 dir, out Vector2 point)
        {
            if (!TouchMode)
            {
                var cam = Camera.main;
                var mouse = Mouse.current;
                if (cam != null && mouse != null)
                {
                    point = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    Vector2 to = point - origin;
                    dir = to.sqrMagnitude > 0.000001f ? to.normalized : fallbackDir;
                    return;
                }
                dir = fallbackDir;
                point = origin + fallbackDir * MaxAimRange;
                return;
            }

            if (Touch.AimActive)
            {
                dir = Touch.AimDir;
                point = origin + dir * Mathf.Lerp(1.5f, MaxAimRange, Touch.AimStrength);
                return;
            }

            var target = NearestEnemy(origin, AutoAimRange);
            if (target != null)
            {
                point = target.position;
                Vector2 to = point - origin;
                dir = to.sqrMagnitude > 0.000001f ? to.normalized : fallbackDir;
                return;
            }

            dir = fallbackDir;
            point = origin + fallbackDir * 3f;
        }

        static Transform NearestEnemy(Vector2 origin, float maxRange)
        {
            Transform best = null;
            float bestDist = maxRange;
            foreach (var enemy in EnemyAI.Active)
            {
                if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead) continue;
                float d = Vector2.Distance(origin, enemy.transform.position);
                if (d < bestDist) { bestDist = d; best = enemy.transform; }
            }
            return best;
        }
    }
}
