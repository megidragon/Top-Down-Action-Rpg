using UnityEngine;

namespace TinyRpg
{
    /// Oveja ambiental: pasea despacio cerca de su punto de origen y a ratos
    /// se para a pastar. Sin combate; solo da vida al mapa.
    [RequireComponent(typeof(Rigidbody2D))]
    public class SheepAmbient : MonoBehaviour
    {
        public float wanderRadius = 2.5f;
        public float moveSpeed = 1.1f;

        Rigidbody2D rb;
        Animator animator;
        SpriteRenderer spriteRenderer;
        Vector2 home;
        Vector2 target;
        float stateTimer;
        bool grazing = true;
        string currentState = "";

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponentInChildren<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        void Start()
        {
            home = transform.position;
            target = home;
            stateTimer = Random.Range(1f, 4f);
        }

        void Update()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                grazing = !grazing;
                if (grazing)
                {
                    stateTimer = Random.Range(2.5f, 6f);
                }
                else
                {
                    target = home + Random.insideUnitCircle * wanderRadius;
                    stateTimer = Random.Range(2f, 4f);
                }
            }
            Play(grazing ? (Random.value < 0.001f ? "Idle" : "Grass") : "Move");
        }

        void FixedUpdate()
        {
            if (grazing)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            Vector2 pos = rb.position;
            Vector2 to = target - pos;
            if (to.magnitude < 0.15f)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            Vector2 dir = to.normalized;
            rb.linearVelocity = dir * moveSpeed;
            if (spriteRenderer != null && Mathf.Abs(dir.x) > 0.05f)
                spriteRenderer.flipX = dir.x < 0f;
        }

        void Play(string state)
        {
            if (state == currentState || animator == null) return;
            currentState = state;
            animator.Play(state, 0, Random.value);
        }
    }
}
