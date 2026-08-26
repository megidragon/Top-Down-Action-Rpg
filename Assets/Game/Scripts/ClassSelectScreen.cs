using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Pantalla inicial de seleccion de clase. El juego queda en pausa
    /// (timeScale 0) hasta que el jugador elige Guerrero o Lancero; Arquero y
    /// Monje se muestran ennegrecidos y no son seleccionables todavia.
    /// Tras morir y pulsar R la escena se recarga y se vuelve a elegir.
    public class ClassSelectScreen : MonoBehaviour
    {
        public static ClassSelectScreen Instance { get; private set; }

        public GameObject panel;
        public GameObject warriorPrefab;
        public GameObject lancerPrefab;
        public Vector2 spawnPosition;
        public SmoothCameraFollow cameraFollow;

        public bool HasChosen { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Time.timeScale = 0f;
            if (panel != null) panel.SetActive(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f; // no dejar el juego pausado al recargar escena
        }

        void Update()
        {
            if (HasChosen) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
        }

        /// 0 = Guerrero, 1 = Lancero.
        public void Choose(int classIndex)
        {
            if (HasChosen) return;
            var prefab = classIndex == 1 ? lancerPrefab : warriorPrefab;
            if (prefab == null) return;

            HasChosen = true;
            var player = Instantiate(prefab, spawnPosition, Quaternion.identity);
            player.name = prefab.name;

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
