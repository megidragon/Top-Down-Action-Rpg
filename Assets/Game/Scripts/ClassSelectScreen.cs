using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Pantalla inicial de seleccion de clase. El juego queda en pausa
    /// (timeScale 0) hasta que el jugador elige una de las 5 clases
    /// (teclas 1-5: Guerrero, Lancero, Arquero, Monje, Mago).
    /// Tras morir y pulsar R la escena se recarga y se vuelve a elegir.
    public class ClassSelectScreen : MonoBehaviour
    {
        public static ClassSelectScreen Instance { get; private set; }

        public GameObject panel;
        public GameObject warriorPrefab;
        public GameObject lancerPrefab;
        public GameObject archerPrefab;
        public GameObject monkPrefab;
        public GameObject magePrefab;
        public Vector2 spawnPosition;
        public SmoothCameraFollow cameraFollow;

        public bool HasChosen { get; private set; }
        public int ChosenClassIndex { get; private set; } = -1;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            // El juego arranca pausado; la pantalla de titulo se muestra primero
            // y llama a Show() al pulsar "Comenzar partida".
            Time.timeScale = 0f;
            if (panel != null) panel.SetActive(false);

            // Tras morir y pulsar R: saltar el titulo e ir directo a elegir
            // clase, un frame despues de que todos los Start hayan corrido.
            if (TitleScreen.SkipTitleOnce)
                StartCoroutine(ShowAfterSkip());
        }

        System.Collections.IEnumerator ShowAfterSkip()
        {
            yield return null; // esperar a que corran todos los Start
            TitleScreen.SkipTitleOnce = false;
            if (TitleScreen.Instance != null && TitleScreen.Instance.panel != null)
                TitleScreen.Instance.panel.SetActive(false);
            Show();
        }

        public void Show()
        {
            if (!HasChosen && panel != null) panel.SetActive(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f; // no dejar el juego pausado al recargar escena
        }

        void Update()
        {
            if (HasChosen || panel == null || !panel.activeSelf) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) Choose(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) Choose(3);
            else if (keyboard.digit5Key.wasPressedThisFrame) Choose(4);
        }

        /// 0 = Guerrero, 1 = Lancero, 2 = Arquero, 3 = Monje, 4 = Mago.
        public void Choose(int classIndex)
        {
            if (HasChosen) return;
            var prefab = classIndex == 4 ? magePrefab
                       : classIndex == 3 ? monkPrefab
                       : classIndex == 2 ? archerPrefab
                       : classIndex == 1 ? lancerPrefab : warriorPrefab;
            if (prefab == null) return;

            HasChosen = true;
            ChosenClassIndex = classIndex;
            var player = Instantiate(prefab, spawnPosition, Quaternion.identity);
            player.name = prefab.name;

            // Equipo inicial de la run: una pocion de vida basica (1 uso).
            player.GetComponent<Inventory>()?.AddItem(ItemType.HealthPotion, 1);

            if (cameraFollow != null)
            {
                cameraFollow.target = player.transform;
                cameraFollow.transform.position =
                    new Vector3(spawnPosition.x, spawnPosition.y, -10f);
            }

            if (panel != null) panel.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
