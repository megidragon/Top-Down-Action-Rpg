using System;
using UnityEngine;

namespace TinyRpg
{
    public enum ItemType
    {
        None = 0,
        Coin = 1,         // no usable; se acumula en un solo slot
        HealthPotion = 2, // usable: restaura 50 de vida
    }

    /// Inventario del jugador: 4 slots (teclas 1-4). Los objetos del mismo tipo
    /// se apilan en un unico slot.
    public class Inventory : MonoBehaviour
    {
        public const int SlotCount = 4;
        public const float PotionHealAmount = 50f;

        [Serializable]
        public struct Slot
        {
            public ItemType type;
            public int count;
        }

        readonly Slot[] slots = new Slot[SlotCount];
        CharacterStats stats;

        /// Las monedas ya no ocupan slot: contador propio con su HUD.
        public int Coins { get; private set; }

        public event Action Changed;

        void Awake()
        {
            stats = GetComponent<CharacterStats>();
        }

        public void AddCoins(int amount)
        {
            Coins += amount;
            Changed?.Invoke();
        }

        public bool TrySpendCoins(int amount)
        {
            if (Coins < amount) return false;
            Coins -= amount;
            Changed?.Invoke();
            return true;
        }

        public void ResetForNewRun()
        {
            Coins = 0;
            for (int i = 0; i < SlotCount; i++) slots[i] = default;
            Changed?.Invoke();
        }

        public Slot GetSlot(int index) => slots[index];

        public int CountOf(ItemType type)
        {
            int total = 0;
            for (int i = 0; i < SlotCount; i++)
                if (slots[i].type == type) total += slots[i].count;
            return total;
        }

        public bool HasFreeSlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (slots[i].type == ItemType.None) return true;
            return false;
        }

        public bool AddItem(ItemType type, int amount = 1)
        {
            if (type == ItemType.None || amount <= 0) return false;

            // Las monedas van a su contador, no a un slot.
            if (type == ItemType.Coin)
            {
                AddCoins(amount);
                return true;
            }

            // Las pociones NO se apilan: cada botella ocupa su propio slot y
            // 'count' son los usos que le quedan (1/2/3 segun el tipo comprado).
            bool stackable = type != ItemType.HealthPotion;
            if (stackable)
            {
                for (int i = 0; i < SlotCount; i++)
                    if (slots[i].type == type)
                    {
                        slots[i].count += amount;
                        Changed?.Invoke();
                        return true;
                    }
            }

            // Ocupar el primer slot libre.
            for (int i = 0; i < SlotCount; i++)
                if (slots[i].type == ItemType.None)
                {
                    slots[i].type = type;
                    slots[i].count = amount;
                    Changed?.Invoke();
                    return true;
                }

            return false; // inventario lleno
        }

        public bool TryRemove(ItemType type, int amount)
        {
            for (int i = 0; i < SlotCount; i++)
                if (slots[i].type == type && slots[i].count >= amount)
                {
                    slots[i].count -= amount;
                    if (slots[i].count <= 0) slots[i] = default;
                    Changed?.Invoke();
                    return true;
                }
            return false;
        }

        /// Intenta usar el objeto del slot (teclas 1-4). Las monedas no son usables.
        public bool UseSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return false;
            var slot = slots[index];
            if (slot.count <= 0) return false;

            switch (slot.type)
            {
                case ItemType.HealthPotion:
                    if (stats == null || stats.IsDead || stats.Health >= stats.maxHealth)
                        return false; // no desperdiciar la pocion con la vida llena
                    stats.Heal(PotionHealAmount);
                    slots[index].count--;
                    if (slots[index].count <= 0) slots[index] = default;
                    Changed?.Invoke();
                    GetComponent<UnitAnimator>()?.FlashHit(new Color(0.5f, 1f, 0.55f, 1f));
                    return true;

                default:
                    return false; // la moneda (y cualquier no-usable) no hace nada
            }
        }
    }
}
