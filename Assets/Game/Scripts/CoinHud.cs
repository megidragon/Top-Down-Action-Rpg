using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Contador de monedas a la derecha de la barra de inventario.
    public class CoinHud : MonoBehaviour
    {
        public Text countText;

        Inventory inventory;

        void Update()
        {
            if (inventory == null)
            {
                if (GameManager.Player == null) return;
                inventory = GameManager.Player.GetComponent<Inventory>();
                if (inventory == null) return;
                inventory.Changed += Refresh;
                Refresh();
            }
        }

        void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= Refresh;
        }

        void Refresh()
        {
            if (countText != null && inventory != null)
                countText.text = inventory.Coins.ToString();
        }
    }
}
