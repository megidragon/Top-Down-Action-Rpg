using UnityEngine;

namespace TinyRpg
{
    /// El tesoro del corazon del bosque: pila de oro con un halo. Tocarlo gana
    /// la run.
    public class TreasureTrigger : MonoBehaviour
    {
        bool consumed;
        GameObject ring;

        public static TreasureTrigger Create(Vector2 pos, Transform parent)
        {
            var lib = MapLibrary.Instance;
            var go = new GameObject("Treasure");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(go.transform, false);
            spriteGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = lib.goldSprites != null && lib.goldSprites.Length > 4
                ? lib.goldSprites[4] : (lib.goldSprites != null && lib.goldSprites.Length > 0
                    ? lib.goldSprites[0] : null);
            sr.sortingOrder = YSorter.OrderForY(pos.y) + 1;

            var treasure = go.AddComponent<TreasureTrigger>();
            treasure.ring = AttackVfx.CreateRing(0.9f, new Color(1f, 0.85f, 0.3f, 0.6f),
                YSorter.OrderForY(pos.y) + 2);
            treasure.ring.transform.SetParent(go.transform, false);
            treasure.ring.transform.localPosition = Vector3.zero;
            return treasure;
        }

        void Update()
        {
            if (ring != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.1f;
                ring.transform.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (consumed) return;
            var player = GameManager.Player;
            if (player == null) return;
            var stats = player.GetComponent<CharacterStats>();
            if (stats == null || stats.IsDead) return;

            if (Vector2.Distance(player.transform.position, transform.position) <= 1.1f)
            {
                consumed = true;
                GameFlow.Instance?.Victory();
            }
        }
    }
}
