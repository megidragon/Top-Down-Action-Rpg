using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// HUD del inventario: 4 slots en la parte inferior con icono, cantidad y tecla.
    public class InventoryHud : MonoBehaviour
    {
        [System.Serializable]
        public struct SlotWidgets
        {
            public Image icon;
            public Text countText;
        }

        public SlotWidgets[] slotWidgets = new SlotWidgets[Inventory.SlotCount];

        Inventory inventory;

        void Start()
        {
            // En movil los huecos se usan tocandolos (no hay teclas 1-4).
            var touch = GameInput.Touch;
            if (touch == null || !touch.Active) return;
            for (int i = 0; i < slotWidgets.Length && i < Inventory.SlotCount; i++)
            {
                var icon = slotWidgets[i].icon;
                // El fondo del hueco es el padre del icono: es la zona tocable.
                if (icon != null && icon.transform.parent is RectTransform frame)
                    touch.AttachItemTap(frame, i);
            }
        }

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
            if (inventory == null) return;
            var library = ItemLibrary.Instance;
            for (int i = 0; i < slotWidgets.Length && i < Inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                var widgets = slotWidgets[i];
                bool hasItem = slot.count > 0 && slot.type != ItemType.None;

                if (widgets.icon != null)
                {
                    widgets.icon.enabled = hasItem;
                    if (hasItem && library != null)
                        widgets.icon.sprite = library.GetIcon(slot.type);
                }
                if (widgets.countText != null)
                    widgets.countText.text = hasItem && slot.count > 1 ? slot.count.ToString() : "";
            }
        }
    }
}
