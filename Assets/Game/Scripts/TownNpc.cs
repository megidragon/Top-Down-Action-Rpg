using UnityEngine;

namespace TinyRpg
{
    /// NPC ambiental de la ciudad (pawn): minero picando, lenador talando,
    /// paseante o quieto. Sin combate.
    public class TownNpc : MonoBehaviour
    {
        public enum Mode { Still, Walker, Miner, Chopper }

        public Mode mode = Mode.Still;

        Animator animator;
        SpriteRenderer spriteRenderer;
        Vector2 home;
        Vector2 target;
        float stateTimer;
        bool walking;
        string currentState = "";

        public static TownNpc Create(Vector2 pos, Transform parent, Mode mode)
        {
            var lib = MapLibrary.Instance;
            if (lib.pawnNpcPrefab == null) return null;
            var go = Instantiate(lib.pawnNpcPrefab, pos, Quaternion.identity, parent);
            var npc = go.GetComponent<TownNpc>();
            if (npc != null) npc.mode = mode;
            return npc;
        }

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void Start()
        {
            home = transform.position;
            target = home;
            stateTimer = Random.Range(1f, 3f);

            switch (mode)
            {
                case Mode.Miner:
                    Play("Mine");
                    if (spriteRenderer != null) spriteRenderer.flipX = false;
                    break;
                case Mode.Chopper:
                    Play("Chop");
                    if (spriteRenderer != null) spriteRenderer.flipX = true;
                    break;
                default:
                    Play("Idle");
                    break;
            }
        }

        void Update()
        {
            if (mode != Mode.Walker) return;

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                walking = !walking;
                if (walking)
                {
                    target = home + Random.insideUnitCircle * 3f;
                    stateTimer = Random.Range(2f, 4f);
                }
                else
                {
                    stateTimer = Random.Range(1.5f, 4f);
                }
            }

            if (walking)
            {
                Vector2 pos = transform.position;
                Vector2 to = target - pos;
                if (to.magnitude > 0.15f)
                {
                    Vector2 dir = to.normalized;
                    transform.position = pos + dir * (1.4f * Time.deltaTime);
                    if (spriteRenderer != null && Mathf.Abs(dir.x) > 0.05f)
                        spriteRenderer.flipX = dir.x < 0f;
                    Play("Run");
                }
                else
                {
                    Play("Idle");
                }
            }
            else
            {
                Play("Idle");
            }
        }

        void Play(string state)
        {
            if (state == currentState || animator == null) return;
            currentState = state;
            animator.Play(state, 0, Random.value);
        }
    }
}
