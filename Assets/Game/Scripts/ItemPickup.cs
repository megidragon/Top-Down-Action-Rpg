using UnityEngine;

namespace TinyRpg
{
    /// Objeto soltado en el mundo (moneda). Flota con un balanceo suave y el
    /// jugador lo lotea simplemente acercandose: el objeto vuela hacia el y se
    /// anade al inventario.
    public class ItemPickup : MonoBehaviour
    {
        public ItemType itemType = ItemType.Coin;
        public float magnetRadius = 1.7f;
        public float collectRadius = 0.4f;
        public float magnetSpeed = 7f;

        SpriteRenderer spriteRenderer;
        Vector2 basePosition;
        float bobPhase;
        float magnetVelocity;

        public static ItemPickup Spawn(ItemType type, Vector2 position)
        {
            var lib = ItemLibrary.Instance;
            var go = new GameObject("Pickup_" + type);
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = lib != null ? lib.GetIcon(type) : null;
            sr.sortingOrder = YSorter.OrderForY(position.y) + 2;

            var pickup = go.AddComponent<ItemPickup>();
            pickup.itemType = type;
            pickup.spriteRenderer = sr;
            go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            return pickup;
        }

        void Start()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            basePosition = transform.position;
            bobPhase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            var player = GameManager.Player;
            if (player == null)
            {
                Bob();
                return;
            }
            var playerStats = player.GetComponent<CharacterStats>();
            if (playerStats == null || playerStats.IsDead)
            {
                Bob();
                return;
            }

            Vector2 playerCenter = (Vector2)player.transform.position + Vector2.up * 0.35f;
            Vector2 pos = transform.position;
            float dist = Vector2.Distance(pos, playerCenter);

            if (dist <= collectRadius)
            {
                var inventory = player.GetComponent<Inventory>();
                if (inventory != null && inventory.AddItem(itemType))
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (dist <= magnetRadius)
            {
                // Iman: acelera hacia el jugador.
                magnetVelocity = Mathf.Min(magnetVelocity + 24f * Time.deltaTime, magnetSpeed);
                Vector2 next = Vector2.MoveTowards(pos, playerCenter, magnetVelocity * Time.deltaTime);
                transform.position = next;
                basePosition = next;
            }
            else
            {
                magnetVelocity = 0f;
                Bob();
            }
        }

        void Bob()
        {
            float y = Mathf.Sin(Time.time * 3f + bobPhase) * 0.07f;
            transform.position = basePosition + new Vector2(0f, y);
        }
    }
}
