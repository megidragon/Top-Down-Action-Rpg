using UnityEngine;

namespace TinyRpg
{
    /// Salida de un mapa: un anillo en el suelo con etiqueta. Bloqueada hasta
    /// limpiar el nivel (en la ciudad y el campamento esta activa desde el
    /// principio). El jugador avanza simplemente caminando dentro.
    public class LevelExit : MonoBehaviour
    {
        public float triggerRadius = 1.2f;

        public bool IsActive => active;

        bool active;
        bool consumed;
        GameObject ring;
        TextMesh label;
        string labelText;

        public static LevelExit Create(Vector2 pos, string labelText, Transform parent)
        {
            var go = new GameObject("LevelExit");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var exit = go.AddComponent<LevelExit>();
            exit.labelText = labelText;
            exit.BuildVisual();
            return exit;
        }

        void BuildVisual()
        {
            int order = YSorter.OrderForY(transform.position.y) + 3;
            ring = AttackVfx.CreateRing(1.1f, new Color(0.6f, 0.6f, 0.6f, 0.4f), order);
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.zero;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            label = textGo.AddComponent<TextMesh>();
            // labelText es una clave de localizacion (exit.*).
            label.text = Loc.T(labelText) + "\n" + Loc.T("exit.locked");
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            label.characterSize = 0.042f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.85f, 0.85f, 0.85f, 0.85f);
            var mr = textGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = label.font.material;
            mr.sortingOrder = 31000;
        }

        void OnEnable()
        {
            Loc.LanguageChanged += RefreshLabel;
        }

        void OnDisable()
        {
            Loc.LanguageChanged -= RefreshLabel;
        }

        void RefreshLabel()
        {
            if (label == null) return;
            label.text = active ? Loc.T(labelText)
                                : Loc.T(labelText) + "\n" + Loc.T("exit.locked");
        }

        public void Activate()
        {
            active = true;
            if (ring != null) Destroy(ring);
            ring = AttackVfx.CreateRing(1.1f, new Color(0.4f, 1f, 0.55f, 0.55f),
                YSorter.OrderForY(transform.position.y) + 3);
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.zero;
            if (label != null) label.text = Loc.T(labelText);
        }

        void Update()
        {
            if (ring != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 3.2f) * 0.07f;
                ring.transform.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (!active || consumed) return;
            var player = GameManager.Player;
            if (player == null) return;
            var stats = player.GetComponent<CharacterStats>();
            if (stats == null || stats.IsDead) return;

            if (Vector2.Distance(player.transform.position, transform.position) <= triggerRadius)
            {
                consumed = true;
                GameFlow.Instance?.Advance();
            }
        }
    }
}
