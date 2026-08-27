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
                statsText.text = Loc.T("stats.none");
                return;
            }
            var stats = player.GetComponent<CharacterStats>();
            var attrs = player.GetComponent<CharacterAttributes>();
            int level = GameFlow.Instance != null ? GameFlow.Instance.CurrentLevel : 0;

            statsText.text =
                $"{player.name.Replace("Player_", "")}\n" +
                $"{Loc.T("stats.health")}  {Mathf.CeilToInt(stats.Health)}/{Mathf.CeilToInt(stats.maxHealth)}\n" +
                $"{Loc.T("stats.energy")}  {Mathf.CeilToInt(stats.Energy)}/{Mathf.CeilToInt(stats.maxEnergy)}\n" +
                (attrs != null
                    ? $"{Loc.T("stats.strength")}  {attrs.strength}\n" +
                      $"{Loc.T("stats.defense")}  {attrs.defense} (-{attrs.defense * 2}%)\n" +
                      $"{Loc.T("stats.speed")}  {attrs.speed} (+{(attrs.speed - 5) * 2}%)\n"
                    : "") +
                $"{Loc.T("stats.zone")}: {(level == 0 ? Loc.T("zone.town") : level.ToString())}";
        }
    }
}
