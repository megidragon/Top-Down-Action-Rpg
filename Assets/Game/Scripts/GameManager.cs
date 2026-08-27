using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Utilidades globales de la run: referencia al jugador, mensajes en
    /// pantalla, cadaveres de enemigos (desvanecer + soltar moneda) y el
    /// reinicio con R al morir o ganar.
    public class GameManager : MonoBehaviour
    {
        public static PlayerController Player { get; private set; }

        public static bool IsGameOver => instance != null && instance.gameOver;

        public Text messageText;

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
            if (messageText != null) messageText.text = "";
        }

        public static void ShowMessage(string text)
        {
            if (instance != null && instance.messageText != null)
                instance.messageText.text = text;
        }

        public static void ClearMessageIf(string text)
        {
            if (instance != null && instance.messageText != null
                && instance.messageText.text == text && !instance.gameOver)
                instance.messageText.text = "";
        }

        /// Fin de la run (victoria del tesoro). R recarga la escena.
        public static void TriggerEnd(string message)
        {
            if (instance == null || instance.gameOver) return;
            instance.gameOver = true;
            ShowMessage(message);
        }

        void OnPlayerDied(PlayerController player)
        {
            if (gameOver) return;
            gameOver = true;
            player.GetComponent<UnitAnimator>()?.SetDeadVisual();
            if (messageText != null)
                messageText.text = Loc.T("msg.death");
        }

        /// Desvanece el cadaver de un enemigo y suelta su moneda.
        public static void HandleEnemyCorpse(GameObject enemy) => HandleCorpse(enemy, dropCoin: true);

        /// Desvanece un cadaver; los enemigos ademas sueltan una moneda
        /// (los aliados caidos no).
        public static void HandleCorpse(GameObject unit, bool dropCoin)
        {
            if (instance == null || unit == null) return;
            if (dropCoin)
                ItemPickup.Spawn(ItemType.Coin, (Vector2)unit.transform.position + Vector2.up * 0.2f);
            instance.StartCoroutine(instance.FadeOutAndDestroy(unit));
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
            if (enemy != null) Destroy(enemy);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && gameOver)
                Reload();
        }

        static void Reload()
        {
            Player = null;
            // Reintentar va directo a elegir clase, sin pasar por el titulo.
            TitleScreen.SkipTitleOnce = true;

            var active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
            {
                SceneManager.LoadScene(active.buildIndex);
                return;
            }
#if UNITY_EDITOR
            // Escena fuera de los ajustes de build (la de pruebas): recargar por
            // ruta. El lab es solo de editor, asi que no lastra la build.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                active.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        }
    }
}
