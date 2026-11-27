using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using NativeInventoryList = Il2CppSystem.Collections.Generic.List<InventoryData>;

namespace ZedZone.BetterReload
{
    /// <summary>
    /// 携带容器递归与多层套包弹药/弹匣统计工具，使用对象指针去重以保证与《更好的套包》无缝共存。
    /// </summary>
    internal static class NestedAmmoHelper
    {
        private const int MaxNestDepth = 12;
        private const int MaxMagazineFits = 2048;

        // 只缓存物品定义的判断结果。库存、弹量和 IL2CPP 对象仍以本次调用为准。
        private static IntPtr _itemManagerPtr;
        private static readonly Dictionary<int, bool> _ammoIdCache = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> _magazineIdCache = new Dictionary<int, bool>();
        private static readonly Dictionary<(int magItemId, IntPtr weaponAttrPtr), bool> _magFitCache =
            new Dictionary<(int magItemId, IntPtr weaponAttrPtr), bool>();

        private sealed class InventoryScratch
        {
            internal readonly List<InventoryData> Inventories = new List<InventoryData>();
            internal readonly HashSet<IntPtr> Visited = new HashSet<IntPtr>();
            internal readonly HashSet<IntPtr> Seen = new HashSet<IntPtr>();
            internal bool InUse;
        }

        [ThreadStatic]
        private static InventoryScratch _scratch;

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
            if (characterData != null)
                CollectCarriedInventories(GetCarriedList(characterData), characterData, result, new HashSet<IntPtr>());
            return result;
        }

        private static NativeInventoryList GetCarriedList(CharacterData characterData)
        {
            try { return characterData.GetCarriedInventoryList(); }
            catch (Exception ex)
            {
                Plugin.LogOnce("collect-carried", ex);
                return null;
            }
        }

        private static void CollectCarriedInventories(
            NativeInventoryList carried,
            CharacterData characterData,
            List<InventoryData> result,
            HashSet<IntPtr> visited)
        {
            if (carried != null)
            {
                for (int i = 0, count = carried.Count; i < count; i++)
                    CollectInventoryRecursive(carried[i], result, visited, 0);
            }

            if (result.Count == 0)
                CollectInventoryRecursive(characterData.inventoryData, result, visited, 0);
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

            for (int i = 0, count = items.Count; i < count; i++)
            {
                var child = items[i]?.inventoryData;
                if (child != null)
                    CollectInventoryRecursive(child, result, visited, depth + 1);
            }
        }

        private static InventoryScratch RentScratch()
        {
            var scratch = _scratch ??= new InventoryScratch();
            // 库存查询可能被其他补丁重入，内层调用不能清空外层正在用的集合。
            if (scratch.InUse)
                return new InventoryScratch();
            scratch.InUse = true;
            return scratch;
        }

        private static void ReturnScratch(InventoryScratch scratch)
        {
            scratch.Inventories.Clear();
            scratch.Visited.Clear();
            scratch.Seen.Clear();
            scratch.InUse = false;
        }

        internal static void ClearMetadataCache()
        {
            _ammoIdCache.Clear();
            _magazineIdCache.Clear();
            _magFitCache.Clear();
        }

        private static ItemManager GetItemManager()
        {
            var itemMgr = ItemManager.instance;
            IntPtr ptr = Key(itemMgr);
            if (ptr != _itemManagerPtr)
            {
                ClearMetadataCache();
                _itemManagerPtr = ptr;
            }
            return itemMgr;
        }

        internal static bool IsAmmoItemId(int itemId)
        {
            if (itemId <= 0) return false;
            var itemMgr = GetItemManager();
            if (itemMgr == null) return false;

            if (!_ammoIdCache.TryGetValue(itemId, out bool isAmmo))
            {
                var attr = itemMgr.GetItemAttrById(itemId);
                isAmmo = attr != null && attr.itemType == ItemType.Ammo;
                _ammoIdCache[itemId] = isAmmo;
            }
            return isAmmo;
        }

        private static bool HasNestedInventory(InventoryData inventory)
        {
            var items = inventory?.itemList;
            if (items == null) return false;

            for (int i = 0, count = items.Count; i < count; i++)
            {
                if (items[i]?.inventoryData != null)
                    return true;
            }
            return false;
        }

        private static bool HasNestedInventory(NativeInventoryList carried, CharacterData characterData)
        {
            bool hasCarried = false;
            if (carried != null)
            {
                for (int i = 0, count = carried.Count; i < count; i++)
                {
                    var inventory = carried[i];
                    if (inventory == null) continue;
                    hasCarried = true;
                    if (HasNestedInventory(inventory))
                        return true;
                }
            }
            return !hasCarried && HasNestedInventory(characterData.inventoryData);
        }

        /// <summary>
        /// 向原版结果列表补充子背包内同 ID 弹药；通过指针集合去重，不会与已有套包模组重复计数。
        /// </summary>
        internal static int AppendNestedAmmoItems(
            CharacterData characterData,
            int itemId,
            Il2CppSystem.Collections.Generic.List<ItemData> result)
        {
            if (!IsAmmoItemId(itemId) || characterData == null || result == null)
                return 0;

            var carried = GetCarriedList(characterData);
            // 普通背包的散弹已由原版统计；没有子容器时无需收集库存或给结果去重。
            if (!HasNestedInventory(carried, characterData)) return 0;

            var scratch = RentScratch();
            try
            {
                for (int i = 0, count = result.Count; i < count; i++)
                {
                    IntPtr ptr = Key(result[i]);
                    if (ptr != IntPtr.Zero) scratch.Seen.Add(ptr);
                }

                int added = 0;
                var inventories = scratch.Inventories;
                CollectCarriedInventories(carried, characterData, inventories, scratch.Visited);
                for (int i = 0; i < inventories.Count; i++)
                {
                    var items = inventories[i].itemList;
                    if (items == null) continue;

                    for (int j = 0, count = items.Count; j < count; j++)
                    {
                        var item = items[j];
                        if (item == null || item.itemId != itemId || item.itemNumberFloat <= 0f)
                            continue;

                        IntPtr ptr = Key(item);
                        if (ptr != IntPtr.Zero && scratch.Seen.Add(ptr))
                        {
                            result.Add(item);
                            added++;
                        }
                    }
                }

                return added;
            }
            finally
            {
                ReturnScratch(scratch);
            }
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
            if (weaponAttr == null || weaponAttr.feedingType != ItemAttr_RangedWeapon.FeedingType.Magazine ||
                character == null)
                return 0;

            var characterData = character.characterData;
            var itemMgr = GetItemManager();
            if (characterData == null || itemMgr == null) return 0;

            var scratch = RentScratch();
            try
            {
                int total = 0;
                IntPtr weaponAttrPtr = weaponAttr.Pointer;
                var inventories = scratch.Inventories;
                CollectCarriedInventories(GetCarriedList(characterData), characterData, inventories, scratch.Visited);
                for (int i = 0; i < inventories.Count; i++)
                {
                    var items = inventories[i].itemList;
                    if (items == null) continue;

                    for (int j = 0, count = items.Count; j < count; j++)
                    {
                        var item = items[j];
                        if (item == null || !IsCompatibleMagazine(itemMgr, item.itemId, weaponAttr, weaponAttrPtr))
                            continue;

                        IntPtr ptr = Key(item);
                        if (ptr == IntPtr.Zero || !scratch.Seen.Add(ptr)) continue;

                        int loaded = ItemData.GetMagazineAmmoNumber(item);
                        if (loaded > 0)
                            total += loaded;
                    }
                }

                return total;
            }
            finally
            {
                ReturnScratch(scratch);
            }
        }

        private static bool IsCompatibleMagazine(
            ItemManager itemMgr, int itemId, ItemAttr_RangedWeapon weaponAttr, IntPtr weaponAttrPtr)
        {
            ItemAttr attr = null;
            if (!_magazineIdCache.TryGetValue(itemId, out bool isMagazine))
            {
                attr = itemMgr.GetItemAttrById(itemId);
                isMagazine = attr != null && attr.itemType == ItemType.Magazine;
                _magazineIdCache[itemId] = isMagazine;
            }
            if (!isMagazine) return false;

            var key = (itemId, weaponAttrPtr);
            if (!_magFitCache.TryGetValue(key, out bool fits))
            {
                var magazineAttr = (attr ?? itemMgr.GetItemAttrById(itemId))?.TryCast<ItemAttr_Magazine>();
                fits = magazineAttr != null && magazineAttr.IsMagazineFit(weaponAttr);
                if (_magFitCache.Count >= MaxMagazineFits)
                    _magFitCache.Clear();
                _magFitCache[key] = fits;
            }
            return fits;
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
