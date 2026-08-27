using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Icono de personaje a la izquierda de la barra de accion: al pasar el
    /// raton por encima despliega un banner con las estadisticas.
    public class PlayerStatsPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject panel;
        public Text statsText;

        void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (panel == null) return;
            RefreshStats();
            panel.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (panel != null && panel.activeSelf) RefreshStats();
        }

        void RefreshStats()
        {
            if (statsText == null) return;
            var player = GameManager.Player;
            if (player == null)
            {
                statsText.text = "Sin personaje";
                return;
            }
            var stats = player.GetComponent<CharacterStats>();
            var attrs = player.GetComponent<CharacterAttributes>();
            int level = GameFlow.Instance != null ? GameFlow.Instance.CurrentLevel : 0;

            statsText.text =
                $"{player.name.Replace("Player_", "")}\n" +
                $"Vida  {Mathf.CeilToInt(stats.Health)}/{Mathf.CeilToInt(stats.maxHealth)}\n" +
                $"Energia  {Mathf.CeilToInt(stats.Energy)}/{Mathf.CeilToInt(stats.maxEnergy)}\n" +
                (attrs != null
                    ? $"Fuerza  {attrs.strength}\n" +
                      $"Defensa  {attrs.defense} (-{attrs.defense * 2}%)\n" +
                      $"Velocidad  {attrs.speed} (+{(attrs.speed - 5) * 2}%)\n"
                    : "") +
                $"Zona: {(level == 0 ? "Ciudad" : "Nivel " + level)}";
        }
    }
}
