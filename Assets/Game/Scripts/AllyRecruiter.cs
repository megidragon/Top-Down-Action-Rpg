using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Punto de reclutamiento del campamento: el propio aliado (unidad azul)
    /// espera de pie junto a la fogata con un cartel de precio. Con E se une
    /// al grupo. El primer reclutamiento de cada hueco (niveles 6/12/18) es
    /// gratis; los sustitutos de aliados caidos cuestan monedas.
    public class AllyRecruiter : MonoBehaviour
    {
        public float interactRadius = 2.4f;

        int price;
        bool recruited;
        AllyAI recruit;
        GameObject bubble;
        SpriteRenderer coinIcon;
        float feedbackTimer;

        public static AllyRecruiter Create(Vector2 pos, Transform parent, int classIndex, int price)
        {
            var go = new GameObject("AllyRecruiter");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var rec = go.AddComponent<AllyRecruiter>();
            rec.price = price;
            rec.recruit = AllyAI.Spawn(classIndex, pos, dormant: true, parent: go.transform);
            rec.BuildBubble(classIndex);
            return rec;
        }

        static string ClassKey(int classIndex)
        {
            switch (classIndex)
            {
                case 1: return "class.lancer";
                case 2: return "class.archer";
                case 3: return "class.monk";
                case 4: return "class.mage";
                default: return "class.warrior";
            }
        }

        void BuildBubble(int classIndex)
        {
            int order = 31000;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            bubble = new GameObject("Bubble");
            bubble.transform.SetParent(transform, false);
            bubble.transform.localPosition = new Vector3(0f, 2.15f, 0f);

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(bubble.transform, false);
            nameGo.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            var nameText = nameGo.AddComponent<TextMesh>();
            nameText.text = Loc.T(ClassKey(classIndex));
            nameText.font = font;
            nameText.fontSize = 64;
            nameText.characterSize = 0.05f;
            nameText.anchor = TextAnchor.MiddleCenter;
            nameText.alignment = TextAlignment.Center;
            nameText.color = new Color(0.75f, 0.95f, 1f, 1f);
            nameGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            nameGo.GetComponent<MeshRenderer>().sortingOrder = order + 1;

            var priceGo = new GameObject("Price");
            priceGo.transform.SetParent(bubble.transform, false);
            var priceText = priceGo.AddComponent<TextMesh>();
            priceText.font = font;
            priceText.fontSize = 64;
            priceText.anchor = TextAnchor.MiddleCenter;
            priceText.alignment = TextAlignment.Center;
            priceGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            priceGo.GetComponent<MeshRenderer>().sortingOrder = order + 1;

            if (price <= 0)
            {
                priceGo.transform.localPosition = Vector3.zero;
                priceText.text = Loc.T("ally.free");
                priceText.characterSize = 0.05f;
                priceText.color = new Color(0.55f, 1f, 0.6f, 1f);
            }
            else
            {
                priceGo.transform.localPosition = new Vector3(-0.12f, 0f, 0f);
                priceText.text = price.ToString();
                priceText.characterSize = 0.055f;
                priceText.color = Color.white;

                var coinGo = new GameObject("Coin");
                coinGo.transform.SetParent(bubble.transform, false);
                coinGo.transform.localPosition = new Vector3(0.32f, 0f, 0f);
                coinGo.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                coinIcon = coinGo.AddComponent<SpriteRenderer>();
                coinIcon.sprite = MapLibrary.Instance.coinHudIcon;
                coinIcon.sortingOrder = order + 1;
            }

            var hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(bubble.transform, false);
            hintGo.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            var hint = hintGo.AddComponent<TextMesh>();
            hint.text = Loc.T("hint.recruit");
            hint.font = font;
            hint.fontSize = 64;
            hint.characterSize = 0.038f;
            hint.anchor = TextAnchor.MiddleCenter;
            hint.alignment = TextAlignment.Center;
            hint.color = new Color(1f, 0.95f, 0.75f, 1f);
            hintGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            hintGo.GetComponent<MeshRenderer>().sortingOrder = order + 1;

            bubble.SetActive(false);
        }

        void Update()
        {
            var player = GameManager.Player;
            bool near = false;
            if (player != null && !recruited && recruit != null)
            {
                var stats = player.GetComponent<CharacterStats>();
                near = stats != null && !stats.IsDead
                    && Vector2.Distance(player.transform.position, transform.position) <= interactRadius;

                if (near)
                {
                    GameInput.Touch?.RequestInteract();
                    if (GameInput.InteractPressed && InteractGate.TryConsume())
                        TryRecruit(player);
                }
            }
            if (bubble != null && bubble.activeSelf != near)
                bubble.SetActive(near);

            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.deltaTime;
                if (feedbackTimer <= 0f && coinIcon != null)
                    coinIcon.color = Color.white;
            }
        }

        void TryRecruit(PlayerController player)
        {
            var inventory = player.GetComponent<Inventory>();
            if (price > 0 && (inventory == null || !inventory.TrySpendCoins(price)))
            {
                if (coinIcon != null)
                {
                    coinIcon.color = new Color(1f, 0.35f, 0.3f, 1f);
                    feedbackTimer = 0.35f;
                }
                return;
            }

            recruited = true;
            recruit.Activate();
            GameFlow.Instance?.OnAllyRecruited(price <= 0);
            AttackVfx.SpawnBlockSpark((Vector2)transform.position + Vector2.up * 1.4f,
                YSorter.OrderForY(transform.position.y) + 6);
            Destroy(gameObject); // el aliado ya se desacoplo con Activate()
        }
    }
}
