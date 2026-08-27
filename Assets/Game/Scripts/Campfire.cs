using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Fogata del campamento: circulo de piedras con fuego animado. Al
    /// interactuar (E) cura por completo al jugador.
    public class Campfire : MonoBehaviour
    {
        public float interactRadius = 2.2f;

        TextMesh hint;

        public static Campfire Create(Vector2 pos, Transform parent)
        {
            var lib = MapLibrary.Instance;
            var go = new GameObject("Campfire");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // Piedras alrededor del fuego (mock de fogata manteniendo el estilo).
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.PI * 2f * i / 7f;
                var rockGo = new GameObject("Stone");
                rockGo.transform.SetParent(go.transform, false);
                rockGo.transform.localPosition =
                    new Vector3(Mathf.Cos(a) * 0.8f, Mathf.Sin(a) * 0.55f - 0.1f, 0f);
                rockGo.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                var rockSr = rockGo.AddComponent<SpriteRenderer>();
                rockSr.sprite = lib.rockSprites != null && lib.rockSprites.Length > 0
                    ? lib.rockSprites[i % lib.rockSprites.Length] : null;
                rockSr.sortingOrder = YSorter.OrderForY(pos.y);
            }

            // Fuego animado del pack, bien visible.
            var fireGo = new GameObject("Fire");
            fireGo.transform.SetParent(go.transform, false);
            fireGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            fireGo.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            var fireSr = fireGo.AddComponent<SpriteRenderer>();
            fireSr.sprite = lib.fireSprite;
            fireSr.sortingOrder = YSorter.OrderForY(pos.y) + 2;
            if (lib.fireController != null)
            {
                var animator = fireGo.AddComponent<Animator>();
                animator.runtimeAnimatorController = lib.fireController;
            }

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;

            var campfire = go.AddComponent<Campfire>();
            campfire.BuildHint();
            return campfire;
        }

        void BuildHint()
        {
            var hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(transform, false);
            hintGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            hint = hintGo.AddComponent<TextMesh>();
            hint.text = Loc.T("hint.rest");
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 64;
            hint.characterSize = 0.04f;
            hint.anchor = TextAnchor.MiddleCenter;
            hint.alignment = TextAlignment.Center;
            hint.color = new Color(1f, 0.9f, 0.6f, 1f);
            var mr = hintGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = hint.font.material;
            mr.sortingOrder = 31000;
            hintGo.SetActive(false);
        }

        void Update()
        {
            var player = GameManager.Player;
            bool near = false;
            if (player != null)
            {
                var stats = player.GetComponent<CharacterStats>();
                near = stats != null && !stats.IsDead
                    && Vector2.Distance(player.transform.position, transform.position) <= interactRadius;

                if (near)
                {
                    var keyboard = Keyboard.current;
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                    {
                        stats.Heal(stats.maxHealth);
                        player.GetComponent<UnitAnimator>()?.FlashHit(new Color(0.55f, 1f, 0.6f, 1f));
                        AttackVfx.SpawnArc(player.transform.position, Vector2.up, 1.1f, 180f,
                            new Color(1f, 0.75f, 0.35f, 0.5f),
                            YSorter.OrderForY(player.transform.position.y) + 5, 0.35f);
                    }
                }
            }
            if (hint != null && hint.gameObject.activeSelf != near)
                hint.gameObject.SetActive(near);
        }
    }
}
