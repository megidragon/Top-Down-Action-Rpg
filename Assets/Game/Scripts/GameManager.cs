using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Estado global: referencia al jugador, recuento de enemigos,
    /// pantalla de muerte (R para reiniciar) y cartel de victoria.
    public class GameManager : MonoBehaviour
    {
        public static PlayerController Player { get; private set; }

        public Text messageText;

        int enemiesAlive;
        bool gameOver;

        static GameManager instance;

        void Awake()
        {
            instance = this;
        }

        public static void RegisterPlayer(PlayerController player)
        {
            Player = player;
            player.GetComponent<CharacterStats>().Died += () => instance?.OnPlayerDied(player);
        }

        void Start()
        {
            // Registrar todos los enemigos presentes en la escena.
            foreach (var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                enemiesAlive++;
                var enemyStats = enemy.GetComponent<CharacterStats>();
                var enemyGo = enemy.gameObject;
                enemyStats.Died += () => OnEnemyDied(enemyGo);
            }
            if (messageText != null) messageText.text = "";
        }

        void OnEnemyDied(GameObject enemy)
        {
            enemiesAlive--;
            StartCoroutine(FadeOutAndDestroy(enemy));
            if (enemiesAlive <= 0 && !gameOver)
            {
                gameOver = true;
                if (messageText != null)
                    messageText.text = "¡VICTORIA!\nHas limpiado la isla.\nPulsa R para volver a jugar";
            }
        }

        void OnPlayerDied(PlayerController player)
        {
            if (gameOver) return;
            gameOver = true;
            var anim = player.GetComponent<UnitAnimator>();
            anim?.SetDeadVisual();
            if (messageText != null)
                messageText.text = "HAS MUERTO\nPulsa R para reintentar";
        }

        IEnumerator FadeOutAndDestroy(GameObject enemy)
        {
            var anim = enemy.GetComponent<UnitAnimator>();
            anim?.SetDeadVisual();

            // Ventana breve para que el empuje del golpe letal desplace el cuerpo.
            yield return new WaitForSeconds(0.35f);
            if (enemy == null) yield break;
            foreach (var col in enemy.GetComponentsInChildren<Collider2D>()) col.enabled = false;
            var rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;

            var sr = enemy.GetComponentInChildren<SpriteRenderer>();
            yield return new WaitForSeconds(0.35f);
            float t = 0f;
            while (t < 0.8f && sr != null)
            {
                t += Time.deltaTime;
                var c = sr.color;
                c.a = 1f - t / 0.8f;
                sr.color = c;
                yield return null;
            }
            Destroy(enemy);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && gameOver)
            {
                Player = null;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}
