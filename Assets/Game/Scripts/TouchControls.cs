using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Controles tactiles para movil. Se construyen en runtime sobre el canvas
    /// del HUD y solo se activan en plataformas tactiles (o forzados desde
    /// Configuracion, util para probarlos en el editor).
    ///
    /// Esquema:
    ///  - Mitad inferior IZQUIERDA: stick flotante de movimiento.
    ///  - Botones a la DERECHA: ataque, especial, parry/curar y dash. Los dos
    ///    de ataque son ARRASTRABLES: manten y arrastra para apuntar a mano
    ///    (el arquero y el mago ven su circulo moverse), suelta para ejecutar.
    ///    Un toque seco auto-apunta al enemigo mas cercano.
    ///  - Botones contextuales: interactuar (si hay algo cerca) y ordenes de
    ///    aliados (si tienes alguno).
    ///  - Los huecos del inventario se usan tocandolos.
    public class TouchControls : MonoBehaviour
    {
        public bool Active { get; private set; }

        // --- Estado continuo ---
        public Vector2 MoveInput { get; private set; }
        public bool AimActive { get; private set; }
        public Vector2 AimDir { get; private set; } = Vector2.right;
        public float AimStrength { get; private set; }

        // --- Pulsaciones (sello de frame: se consumen una vez, sin perderse
        //     aunque el EventSystem procese el toque tras el Update) ---
        readonly int[] pressFrame = new int[16];
        int primaryUpFrame = -1;

        int interactRequestFrame = -2;
        float dragRadius = 150f;

        RectTransform root;
        GameObject interactButton;
        GameObject allyAttackButton;
        GameObject allyFleeButton;
        static Sprite discSprite;
        static Sprite ringSprite;

        void Awake()
        {
            for (int i = 0; i < pressFrame.Length; i++) pressFrame[i] = -1;

            Active = ShouldBeActive();
            GameInput.Touch = this;
            if (!Active) return;

            dragRadius = Mathf.Max(90f, Screen.height * 0.16f);
            HideKeyboardHints();
            BuildUi();
        }

        void OnDestroy()
        {
            if (GameInput.Touch == this) GameInput.Touch = null;
        }

        /// Forzado solo en memoria (verificacion en el editor): a diferencia del
        /// ajuste de Configuracion, NO se guarda en PlayerPrefs.
        public static bool ForceOverride;

        static bool ShouldBeActive()
        {
            return Application.isMobilePlatform || ForceOverride || GameSettings.ForceTouchControls;
        }

        void Update()
        {
            if (!Active || root == null) return;

            // Con el juego pausado (menu, seleccion de clase) el HUD tactil
            // estorbaria a los paneles: se oculta.
            bool playing = Time.timeScale > 0f;
            if (root.gameObject.activeSelf != playing)
            {
                root.gameObject.SetActive(playing);
                if (!playing) { MoveInput = Vector2.zero; AimActive = false; }
            }
            if (!playing) return;

            bool showInteract = Time.frameCount - interactRequestFrame <= 1;
            if (interactButton != null && interactButton.activeSelf != showInteract)
                interactButton.SetActive(showInteract);

            bool hasAllies = AllyAI.Active.Count > 0;
            if (allyAttackButton != null && allyAttackButton.activeSelf != hasAllies)
                allyAttackButton.SetActive(hasAllies);
            if (allyFleeButton != null && allyFleeButton.activeSelf != hasAllies)
                allyFleeButton.SetActive(hasAllies);
        }

        // ----------------------------------------------------------------
        //  Entrada desde los botones / stick
        // ----------------------------------------------------------------

        public void SetMove(Vector2 move) => MoveInput = move;

        /// Lo llama cualquier interactuable cuando el jugador esta a su alcance.
        public void RequestInteract() => interactRequestFrame = Time.frameCount;

        public void ActionDown(TouchAction action, bool aimable)
        {
            pressFrame[(int)action] = Time.frameCount;
            if (aimable)
            {
                AimActive = false;   // aun sin arrastre: auto-apuntado
                AimStrength = 0f;
            }
        }

        public void ActionDrag(Vector2 dragPixels)
        {
            float len = dragPixels.magnitude;
            if (len < 20f) { AimActive = false; return; }
            AimActive = true;
            AimDir = dragPixels / len;
            AimStrength = Mathf.Clamp01(len / dragRadius);
        }

        public void ActionUp(TouchAction action, bool aimable)
        {
            if (action == TouchAction.Primary) primaryUpFrame = Time.frameCount;
            if (aimable)
            {
                // El apuntado se mantiene un instante para que la accion que se
                // dispara al soltar (lluvia del arquero) use la direccion final.
                AimActive = false;
            }
        }

        // ----------------------------------------------------------------
        //  Consumo de pulsaciones
        // ----------------------------------------------------------------

        bool Consume(TouchAction action)
        {
            int i = (int)action;
            if (pressFrame[i] < 0) return false;
            bool fresh = Time.frameCount - pressFrame[i] <= 1;
            pressFrame[i] = -1;
            return fresh;
        }

        public bool ConsumePrimaryDown() => Consume(TouchAction.Primary);
        public bool ConsumeSecondaryDown() => Consume(TouchAction.Secondary);
        public bool ConsumeSpecial() => Consume(TouchAction.Special);
        public bool ConsumeDash() => Consume(TouchAction.Dash);
        public bool ConsumeInteract() => Consume(TouchAction.Interact);
        public bool ConsumeAllyAttack() => Consume(TouchAction.AllyAttack);
        public bool ConsumeAllyFlee() => Consume(TouchAction.AllyFlee);

        public bool ConsumeItem(int slot)
        {
            if (slot < 0 || slot > 3) return false;
            return Consume(TouchAction.Item0 + slot);
        }

        public bool ConsumePrimaryUp()
        {
            if (primaryUpFrame < 0) return false;
            bool fresh = Time.frameCount - primaryUpFrame <= 1;
            primaryUpFrame = -1;
            return fresh;
        }

        /// Convierte un hueco del inventario en zona tactil (lo pide InventoryHud).
        public void AttachItemTap(RectTransform slot, int index)
        {
            if (!Active || slot == null || index < 0 || index > 3) return;
            var img = slot.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            var tap = slot.gameObject.AddComponent<TouchActionButton>();
            tap.owner = this;
            tap.action = TouchAction.Item0 + index;
            tap.aimable = false;
        }

        // ----------------------------------------------------------------
        //  Construccion de la interfaz
        // ----------------------------------------------------------------

        /// La linea "WASD mover | Shift dash | ..." no aplica en movil.
        void HideKeyboardHints()
        {
            foreach (var loc in GetComponentsInChildren<LocText>(true))
                if (loc.key == "hud.controls" || loc.key == "lab.keys")
                    loc.gameObject.SetActive(false);
        }

        void BuildUi()
        {
            var rootGo = new GameObject("TouchUI", typeof(RectTransform));
            rootGo.transform.SetParent(transform, false);
            root = (RectTransform)rootGo.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            BuildStick();

            // Arco para el pulgar derecho. Diametros generosos: por debajo de
            // ~200 px de referencia el dedo no acierta comodamente.
            MakeButton(TouchAction.Primary, "touch.attack", new Vector2(-200f, 200f), 230f,
                new Color(0.95f, 0.45f, 0.35f, 0.5f), aimable: true);
            MakeButton(TouchAction.Secondary, "touch.special", new Vector2(-440f, 290f), 190f,
                new Color(0.55f, 0.65f, 1f, 0.5f), aimable: true);
            MakeButton(TouchAction.Special, "touch.parry", new Vector2(-235f, 470f), 175f,
                new Color(0.6f, 1f, 0.7f, 0.5f), aimable: false);
            MakeButton(TouchAction.Dash, "touch.dash", new Vector2(-490f, 540f), 150f,
                new Color(1f, 0.9f, 0.45f, 0.5f), aimable: false);

            interactButton = MakeButton(TouchAction.Interact, "touch.interact",
                new Vector2(-250f, 700f), 165f, new Color(1f, 0.85f, 0.5f, 0.55f), aimable: false);
            interactButton.SetActive(false);

            // Ordenes de aliados arriba a la derecha (uso puntual, lejos del
            // arco de accion para no pulsarlas sin querer).
            allyAttackButton = MakeButtonTopRight(TouchAction.AllyAttack, "touch.ally_attack",
                new Vector2(-95f, -170f), 135f, new Color(1f, 0.6f, 0.6f, 0.45f));
            allyFleeButton = MakeButtonTopRight(TouchAction.AllyFlee, "touch.ally_flee",
                new Vector2(-95f, -320f), 135f, new Color(0.7f, 0.85f, 1f, 0.45f));
            allyAttackButton.SetActive(false);
            allyFleeButton.SetActive(false);
        }

        void BuildStick()
        {
            // Zona sensible: mitad izquierda SIN la franja superior, para no
            // tapar el engranaje de pausa.
            var zoneGo = new GameObject("MoveZone", typeof(RectTransform));
            zoneGo.transform.SetParent(root, false);
            var zoneRt = (RectTransform)zoneGo.transform;
            zoneRt.anchorMin = new Vector2(0f, 0f);
            zoneRt.anchorMax = new Vector2(0.5f, 0.82f);
            zoneRt.offsetMin = Vector2.zero;
            zoneRt.offsetMax = Vector2.zero;
            var zoneImg = zoneGo.AddComponent<Image>();
            zoneImg.color = new Color(0f, 0f, 0f, 0.001f); // invisible pero clicable

            var baseGo = MakeDisc("StickBase", root, 280f,
                new Color(1f, 1f, 1f, 0.16f), ring: true);
            var knobGo = MakeDisc("StickKnob", root, 120f,
                new Color(1f, 1f, 1f, 0.36f), ring: false);
            // El stick solo se dibuja: los toques los recibe la zona.
            baseGo.GetComponent<Image>().raycastTarget = false;
            knobGo.GetComponent<Image>().raycastTarget = false;

            var zone = zoneGo.AddComponent<TouchStickZone>();
            zone.owner = this;
            zone.stickBase = (RectTransform)baseGo.transform;
            zone.stickKnob = (RectTransform)knobGo.transform;
            zone.radius = 140f;
        }

        GameObject MakeButtonTopRight(TouchAction action, string locKey, Vector2 pos,
            float size, Color color)
        {
            var go = MakeButton(action, locKey, pos, size, color, aimable: false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = pos;
            return go;
        }

        GameObject MakeButton(TouchAction action, string locKey, Vector2 posFromBottomRight,
            float size, Color color, bool aimable)
        {
            var go = MakeDisc(action.ToString(), root, size, color, ring: true);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = posFromBottomRight;

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var text = label.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(size * 0.23f);
            text.alignment = TextAnchor.MiddleCenter;
            // Sin ajuste de linea: etiquetas como "PARRY" partian en dos.
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(1f, 1f, 1f, 0.95f);
            text.raycastTarget = false;
            text.text = Loc.T(locKey);
            label.AddComponent<LocText>().key = locKey;
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var button = go.AddComponent<TouchActionButton>();
            button.owner = this;
            button.action = action;
            button.aimable = aimable;
            return go;
        }

        GameObject MakeDisc(string name, Transform parent, float size, Color color, bool ring)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = ring ? RingSprite() : DiscSprite();
            img.color = color;
            img.raycastTarget = true;
            return go;
        }

        // Circulos generados en runtime (el pack no trae botones redondos).
        static Sprite DiscSprite()
        {
            if (discSprite == null) discSprite = MakeCircle(false);
            return discSprite;
        }

        static Sprite RingSprite()
        {
            if (ringSprite == null) ringSprite = MakeCircle(true);
            return ringSprite;
        }

        static Sprite MakeCircle(bool ring)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float half = size / 2f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f)
                                       + (y - half + 0.5f) * (y - half + 0.5f)) / half;
                    float a;
                    if (ring)
                    {
                        // Relleno suave + borde marcado.
                        float body = Mathf.Clamp01((0.98f - d) / 0.04f) * 0.55f;
                        float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) / 0.09f);
                        a = Mathf.Clamp01(body + edge);
                    }
                    else
                    {
                        a = Mathf.Clamp01((0.98f - d) / 0.05f);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
