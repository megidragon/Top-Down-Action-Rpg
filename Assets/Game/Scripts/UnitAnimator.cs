using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// Controla el Animator del sprite (Idle/Run/Attack1/Attack2/Guard),
    /// el volteo horizontal segun la direccion de apuntado y el flash al recibir dano.
    public class UnitAnimator : MonoBehaviour
    {
        public Animator animator;
        public SpriteRenderer spriteRenderer;

        CharacterMotor motor;
        string currentState = "";
        float actionLockTimer;
        Coroutine flashRoutine;
        Color baseColor = Color.white;
        bool isDead;

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        void Update()
        {
            if (actionLockTimer > 0f)
            {
                actionLockTimer -= Time.deltaTime;
                return;
            }
            bool moving = motor != null && (motor.MoveInput.sqrMagnitude > 0.01f || motor.IsDashing);
            PlayState(moving ? "Run" : "Idle");
        }

        public void SetFacing(float x)
        {
            if (spriteRenderer == null || Mathf.Abs(x) < 0.01f) return;
            spriteRenderer.flipX = x < 0f;
        }

        /// Reproduce un estado de accion (ataque/guardia) y bloquea Idle/Run durante 'duration'.
        public void PlayAction(string state, float duration)
        {
            if (isDead) return;
            actionLockTimer = duration;
            currentState = state;
            if (animator != null && animator.isActiveAndEnabled)
                animator.Play(state, 0, 0f);
        }

        public void ClearAction()
        {
            if (isDead) return;
            actionLockTimer = 0f;
        }

        void PlayState(string state)
        {
            if (state == currentState) return;
            currentState = state;
            if (animator != null && animator.isActiveAndEnabled)
                animator.Play(state, 0, 0f);
        }

        public void FlashHit(Color color)
        {
            if (spriteRenderer == null || isDead) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        IEnumerator FlashRoutine(Color color)
        {
            spriteRenderer.color = color;
            yield return new WaitForSeconds(0.12f);
            spriteRenderer.color = baseColor;
            flashRoutine = null;
        }

        public void SetDeadVisual()
        {
            isDead = true;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            actionLockTimer = float.MaxValue;
            currentState = "Dead";
            if (animator != null && animator.isActiveAndEnabled)
                animator.Play("Idle", 0, 0f);
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        }
    }
}
