using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Mercader de la parada de descanso: un puesto (mesa de madera + pawn) que
    /// vende UN item aleatorio. Pociones de 1/2/3 usos (1/3/6 monedas) o
    /// elixires permanentes de fuerza/defensa/velocidad (2 monedas antes del
    /// nivel 6; despues +1 cada 3 niveles). Los elixires tambien suben la
    /// estadistica de los aliados vivos.
    public class RestVendor : MonoBehaviour
    {
        public enum Offer
        {
            PotionSmall, PotionMedium, PotionLarge,
            ElixirStrength, ElixirDefense, ElixirSpeed,
        }

        public float interactRadius = 2.4f;

        Offer offer;
        int price;
        bool sold;
        GameObject bubble;
        SpriteRenderer offerIcon;
        SpriteRenderer coinIcon;
        TextMesh priceText;
        float feedbackTimer;

        public static RestVendor Create(Vector2 pos, Transform parent, bool forceBasicPotion)
        {
            var lib = MapLibrary.Instance;
            var go = new GameObject("RestVendor");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // Puesto: el pawn mercader a la vista con un tocon como mostrador.
            if (lib.pawnNpcPrefab != null)
            {
                var pawn = Instantiate(lib.pawnNpcPrefab, pos, Quaternion.identity, go.transform);
                var npc = pawn.GetComponent<TownNpc>();
                if (npc != null) npc.mode = TownNpc.Mode.Still;
            }

            if (lib.stumpSprites != null && lib.stumpSprites.Length > 0)
            {
                var stumpGo = new GameObject("Counter");
                stumpGo.transform.SetParent(go.transform, false);
                stumpGo.transform.localPosition = new Vector3(0.85f, -0.15f, 0f);
                var stumpSr = stumpGo.AddComponent<SpriteRenderer>();
                stumpSr.sprite = lib.stumpSprites[0];
                stumpSr.sortingOrder = YSorter.OrderForY(pos.y - 0.15f);
            }

            var vendor = go.AddComponent<RestVendor>();
            vendor.offer = forceBasicPotion
                ? Offer.PotionSmall
                : (Offer)Random.Range(0, 6);
            vendor.price = PriceOf(vendor.offer);
            vendor.BuildBubble();
            return vendor;
        }

        static int PriceOf(Offer offer)
        {
            switch (offer)
            {
                case Offer.PotionSmall: return 1;
                case Offer.PotionMedium: return 3;
                case Offer.PotionLarge: return 6;
                default: // elixires: precio segun la profundidad de la expedicion
                    return Difficulty.ElixirPriceFor(GameFlow.Instance != null
                        ? GameFlow.Instance.CurrentLevel : 1);
            }
        }

        Sprite IconOf(Offer o)
        {
            var lib = MapLibrary.Instance;
            switch (o)
            {
                case Offer.PotionSmall: return lib.potionSmallIcon;
                case Offer.PotionMedium: return lib.potionMediumIcon;
                case Offer.PotionLarge: return lib.potionLargeIcon;
                case Offer.ElixirStrength: return lib.elixirStrengthIcon;
                case Offer.ElixirDefense: return lib.elixirDefenseIcon;
                default: return lib.elixirSpeedIcon;
            }
        }

        void BuildBubble()
        {
            int order = 31000;
            bubble = new GameObject("Bubble");
            bubble.transform.SetParent(transform, false);
            bubble.transform.localPosition = new Vector3(0f, 1.9f, 0f);

            var lib = MapLibrary.Instance;

            var bg = new GameObject("Bg");
            bg.transform.SetParent(bubble.transform, false);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = lib.potionSmallIcon != null ? null : null; // fondo via escala de blanco
            bg.SetActive(false);

            var iconGo = new GameObject("Offer");
            iconGo.transform.SetParent(bubble.transform, false);
            iconGo.transform.localPosition = new Vector3(-0.45f, 0f, 0f);
            iconGo.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            offerIcon = iconGo.AddComponent<SpriteRenderer>();
            offerIcon.sprite = IconOf(offer);
            offerIcon.sortingOrder = order + 1;

            var priceGo = new GameObject("Price");
            priceGo.transform.SetParent(bubble.transform, false);
            priceGo.transform.localPosition = new Vector3(0.12f, 0f, 0f);
            priceText = priceGo.AddComponent<TextMesh>();
            priceText.text = price.ToString();
            priceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            priceText.fontSize = 64;
            priceText.characterSize = 0.055f;
            priceText.anchor = TextAnchor.MiddleCenter;
            priceText.alignment = TextAlignment.Center;
            priceText.color = Color.white;
            var mr = priceGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = priceText.font.material;
            mr.sortingOrder = order + 1;

            var coinGo = new GameObject("Coin");
            coinGo.transform.SetParent(bubble.transform, false);
            coinGo.transform.localPosition = new Vector3(0.55f, 0f, 0f);
            coinGo.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            coinIcon = coinGo.AddComponent<SpriteRenderer>();
            coinIcon.sprite = lib.coinHudIcon;
            coinIcon.sortingOrder = order + 1;

            var hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(bubble.transform, false);
            hintGo.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            var hint = hintGo.AddComponent<TextMesh>();
            hint.text = Loc.T("hint.buy");
            hint.font = priceText.font;
            hint.fontSize = 64;
            hint.characterSize = 0.038f;
            hint.anchor = TextAnchor.MiddleCenter;
            hint.alignment = TextAlignment.Center;
            hint.color = new Color(1f, 0.95f, 0.75f, 1f);
            var hmr = hintGo.GetComponent<MeshRenderer>();
            hmr.sharedMaterial = hint.font.material;
            hmr.sortingOrder = order + 1;

            bubble.SetActive(false);
        }

        void Update()
        {
            var player = GameManager.Player;
            bool near = false;
            if (player != null && !sold)
            {
                var stats = player.GetComponent<CharacterStats>();
                near = stats != null && !stats.IsDead
                    && Vector2.Distance(player.transform.position, transform.position) <= interactRadius;

                if (near)
                {
                    var keyboard = Keyboard.current;
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame
                        && InteractGate.TryConsume())
                        TryBuy(player);
                }
            }
            if (bubble != null && bubble.activeSelf != near)
                bubble.SetActive(near);

            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.deltaTime;
                if (feedbackTimer <= 0f)
                {
                    if (coinIcon != null) coinIcon.color = Color.white;
                    if (offerIcon != null) offerIcon.color = Color.white;
                }
            }
        }

        /// Aplica un elixir al jugador y a todos los aliados vivos (los aliados
        /// ganan la misma estadistica cuando el jugador consume un elixir).
        static void BuffParty(PlayerController player, System.Action<CharacterAttributes> buff)
        {
            var attrs = player.GetComponent<CharacterAttributes>();
            if (attrs != null) buff(attrs);
            player.GetComponent<CharacterMotor>()?.RefreshAttributesCache();

            foreach (var ally in AllyAI.Active)
            {
                if (ally == null || ally.Stats == null || ally.Stats.IsDead) continue;
                var allyAttrs = ally.GetComponent<CharacterAttributes>();
                if (allyAttrs == null) continue;
                buff(allyAttrs);
                ally.GetComponent<CharacterMotor>()?.RefreshAttributesCache();
                ally.GetComponent<UnitAnimator>()?.FlashHit(new Color(0.6f, 1f, 0.7f, 1f));
            }
        }

        void TryBuy(PlayerController player)
        {
            var inventory = player.GetComponent<Inventory>();
            if (inventory == null) return;

            // Sin hueco libre no hay compra de pociones (y no se cobra nada).
            bool needsSlot = offer == Offer.PotionSmall || offer == Offer.PotionMedium
                || offer == Offer.PotionLarge;
            if (needsSlot && !inventory.HasFreeSlot())
            {
                if (offerIcon != null)
                {
                    offerIcon.color = new Color(1f, 0.35f, 0.3f, 1f);
                    feedbackTimer = 0.35f;
                }
                return;
            }

            if (!inventory.TrySpendCoins(price))
            {
                if (coinIcon != null)
                {
                    coinIcon.color = new Color(1f, 0.35f, 0.3f, 1f);
                    feedbackTimer = 0.35f;
                }
                return;
            }

            switch (offer)
            {
                case Offer.PotionSmall: inventory.AddItem(ItemType.HealthPotion, 1); break;
                case Offer.PotionMedium: inventory.AddItem(ItemType.HealthPotion, 2); break;
                case Offer.PotionLarge: inventory.AddItem(ItemType.HealthPotion, 3); break;
                case Offer.ElixirStrength:
                    BuffParty(player, a => a.AddStrength(1)); break;
                case Offer.ElixirDefense:
                    BuffParty(player, a => a.AddDefense(1)); break;
                case Offer.ElixirSpeed:
                    BuffParty(player, a => a.AddSpeed(1)); break;
            }

            sold = true;
            player.GetComponent<UnitAnimator>()?.FlashHit(new Color(0.6f, 1f, 0.7f, 1f));
            AttackVfx.SpawnBlockSpark((Vector2)transform.position + Vector2.up * 1.4f,
                YSorter.OrderForY(transform.position.y) + 6);
            if (bubble != null) bubble.SetActive(false);
        }
    }
}
