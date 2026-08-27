using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// NPC neutral vendedor. Cuando el jugador se acerca, muestra una burbuja
    /// sobre su cabeza con el producto que vende y su precio en monedas.
    /// El jugador compra con la tecla E.
    public class VendorNpc : MonoBehaviour
    {
        public ItemType itemSold = ItemType.HealthPotion;
        public int priceInCoins = 1;
        public float interactRadius = 2.4f;

        public GameObject bubble;              // raiz de la burbuja (se activa al acercarse)
        public SpriteRenderer coinIconRenderer; // para el feedback de "sin monedas"
        public SpriteRenderer spriteRenderer;   // sprite del pawn (volteo hacia el jugador)

        float feedbackTimer;

        void Start()
        {
            if (bubble != null) bubble.SetActive(false);
        }

        void Update()
        {
            var player = GameManager.Player;
            bool playerNear = false;

            if (player != null)
            {
                var stats = player.GetComponent<CharacterStats>();
                playerNear = stats != null && !stats.IsDead
                    && Vector2.Distance(transform.position, player.transform.position) <= interactRadius;
            }

            if (bubble != null && bubble.activeSelf != playerNear)
                bubble.SetActive(playerNear);

            if (playerNear)
            {
                // Mirar hacia el jugador.
                if (spriteRenderer != null)
                {
                    float dx = player.transform.position.x - transform.position.x;
                    if (Mathf.Abs(dx) > 0.05f) spriteRenderer.flipX = dx < 0f;
                }

                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame
                    && InteractGate.TryConsume())
                    TryBuy(player);
            }

            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.deltaTime;
                if (feedbackTimer <= 0f && coinIconRenderer != null)
                    coinIconRenderer.color = Color.white;
            }
        }

        void TryBuy(PlayerController player)
        {
            var inventory = player.GetComponent<Inventory>();
            if (inventory == null) return;

            if (inventory.CountOf(ItemType.Coin) >= priceInCoins
                && inventory.TryRemove(ItemType.Coin, priceInCoins)
                && inventory.AddItem(itemSold))
            {
                // Compra correcta: chispa sobre el NPC.
                AttackVfx.SpawnBlockSpark((Vector2)transform.position + Vector2.up * 1.1f,
                    YSorter.OrderForY(transform.position.y) + 6);
            }
            else
            {
                // Sin monedas (o inventario lleno): la moneda del cartel parpadea en rojo.
                if (coinIconRenderer != null)
                {
                    coinIconRenderer.color = new Color(1f, 0.35f, 0.3f, 1f);
                    feedbackTimer = 0.35f;
                }
            }
        }
    }
}
