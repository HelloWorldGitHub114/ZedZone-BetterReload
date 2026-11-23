using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;

namespace ZedZone.BetterReload
{
    /// <summary>
    /// 携带容器递归与多层套包弹药/弹匣统计工具，使用对象指针去重以保证与《更好的套包》无缝共存。
    /// </summary>
    internal static class NestedAmmoHelper
    {
        private const int MaxNestDepth = 12;

        internal static IntPtr Key(Il2CppObjectBase obj)
        {
            return obj == null ? IntPtr.Zero : obj.Pointer;
        }

        /// <summary>
        /// 收集角色携带的所有容器（主背包、穿戴装备容器及任意深度的子背包），按指针去重。
        /// </summary>
        internal static List<InventoryData> CollectCarriedInventories(CharacterData characterData)
        {
            var result = new List<InventoryData>();
            if (characterData == null) return result;

            var visited = new HashSet<IntPtr>();

            try
            {
                var carried = characterData.GetCarriedInventoryList();
                if (carried != null)
                {
                    for (int i = 0; i < carried.Count; i++)
                    {
                        CollectInventoryRecursive(carried[i], result, visited, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("collect-carried", ex);
            }

            if (result.Count == 0 && characterData.inventoryData != null)
            {
                CollectInventoryRecursive(characterData.inventoryData, result, visited, 0);
            }

            return result;
        }

        private static void CollectInventoryRecursive(
            InventoryData inv,
            List<InventoryData> result,
            HashSet<IntPtr> visited,
            int depth)
        {
            if (inv == null || depth > MaxNestDepth) return;
            IntPtr ptr = Key(inv);
            if (ptr == IntPtr.Zero || !visited.Add(ptr)) return;

            result.Add(inv);

            var items = inv.itemList;
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && item.inventoryData != null)
                {
                    CollectInventoryRecursive(item.inventoryData, result, visited, depth + 1);
                }
            }
        }

        internal static bool IsAmmoItemId(int itemId)
        {
            if (itemId <= 0) return false;
            var itemMgr = ItemManager.instance;
            if (itemMgr == null) return false;

            var attr = itemMgr.GetItemAttrById(itemId);
            return attr != null && attr.itemType == ItemType.Ammo;
        }

        /// <summary>
        /// 向原版结果列表补充子背包内同 ID 弹药；通过指针集合去重，不会与已有套包模组重复计数。
        /// </summary>
        internal static int AppendNestedAmmoItems(
            CharacterData characterData,
            int itemId,
            Il2CppSystem.Collections.Generic.List<ItemData> result)
        {
            if (characterData == null || result == null || !IsAmmoItemId(itemId))
                return 0;

            var seen = new HashSet<IntPtr>();
            for (int i = 0; i < result.Count; i++)
            {
                IntPtr ptr = Key(result[i]);
                if (ptr != IntPtr.Zero) seen.Add(ptr);
            }

            int added = 0;
            var inventories = CollectCarriedInventories(characterData);
            for (int i = 0; i < inventories.Count; i++)
            {
                var items = inventories[i].itemList;
                if (items == null) continue;

                for (int j = 0; j < items.Count; j++)
                {
                    var item = items[j];
                    if (item == null || item.itemId != itemId || item.itemNumberFloat <= 0f)
                        continue;

                    IntPtr ptr = Key(item);
                    if (ptr != IntPtr.Zero && seen.Add(ptr))
                    {
                        result.Add(item);
                        added++;
                    }
                }
            }

            return added;
        }

        /// <summary>
        /// 在角色所有携带容器（含多层子背包）中查找指定物品所在的归属容器。
        /// </summary>
        internal static InventoryData FindOwnerInventoryRecursive(CharacterData characterData, ItemData target)
        {
            if (characterData == null || target == null) return null;

            IntPtr targetPtr = Key(target);
            if (targetPtr == IntPtr.Zero) return null;

            var inventories = CollectCarriedInventories(characterData);
            for (int i = 0; i < inventories.Count; i++)
            {
                var inv = inventories[i];
                var items = inv.itemList;
                if (items == null) continue;

                for (int j = 0; j < items.Count; j++)
                {
                    if (Key(items[j]) == targetPtr)
                        return inv;
                }
            }

            return null;
        }

        /// <summary>
        /// 判断目标容器是否属于角色当前携带的容器树（含多层子背包）。
        /// </summary>
        internal static bool IsCarriedInventory(CharacterData characterData, InventoryData targetInventory)
        {
            if (characterData == null || targetInventory == null) return false;

            IntPtr targetPtr = Key(targetInventory);
            if (targetPtr == IntPtr.Zero) return false;

            var inventories = CollectCarriedInventories(characterData);
            for (int i = 0; i < inventories.Count; i++)
            {
                if (Key(inventories[i]) == targetPtr)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 当原版 RemoveItemFromCharacter 只查一层导致未能扣掉子背包弹药时，定位其真实归属容器并移除。
        /// </summary>
        internal static bool TryRemoveNestedItem(CharacterData characterData, ItemData target, bool executeOnChange)
        {
            var owner = FindOwnerInventoryRecursive(characterData, target);
            return owner != null && owner.RemoveItem(target, executeOnChange);
        }

        /// <summary>
        /// 统计角色所有随身容器（含多层套包）中兼容指定武器的备用弹匣内已装填的子弹总数（按指针去重）。
        /// </summary>
        internal static int CountCompatibleMagazineAmmo(BasicCharacterController character, ItemAttr_RangedWeapon weaponAttr)
        {
            if (character == null || character.characterData == null || weaponAttr == null)
                return 0;

            var itemMgr = ItemManager.instance;
            if (itemMgr == null) return 0;

            int total = 0;
            var seen = new HashSet<IntPtr>();
            var inventories = CollectCarriedInventories(character.characterData);
            for (int i = 0; i < inventories.Count; i++)
            {
                var items = inventories[i]?.itemList;
                if (items == null) continue;

                for (int j = 0; j < items.Count; j++)
                {
                    var item = items[j];
                    if (item == null) continue;

                    IntPtr ptr = Key(item);
                    if (ptr == IntPtr.Zero || !seen.Add(ptr)) continue;

                    var attr = itemMgr.GetItemAttrById(item.itemId);
                    if (attr == null || attr.itemType != ItemType.Magazine) continue;

                    var magAttr = attr.TryCast<ItemAttr_Magazine>();
                    if (magAttr == null || !magAttr.IsMagazineFit(weaponAttr)) continue;

                    int loaded = ItemData.GetMagazineAmmoNumber(item);
                    if (loaded > 0)
                        total += loaded;
                }
            }

            return total;
        }

        /// <summary>
        /// 压弹结束后对所有携带容器执行空弹药清理，防止子背包内残留 0 发空栈。
        /// </summary>
        internal static void CleanEmptyAmmoInAllCarried(CharacterData characterData)
        {
            if (characterData == null) return;

            var inventories = CollectCarriedInventories(characterData);
            for (int i = 0; i < inventories.Count; i++)
            {
                try
                {
                    inventories[i].RemoveEmptyAmmo();
                }
                catch (Exception ex)
                {
                    Plugin.LogOnce("clean-empty-ammo", ex);
                }
            }
        }
    }
}
