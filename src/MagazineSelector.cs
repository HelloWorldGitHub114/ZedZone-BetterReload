using System;
using System.Collections.Generic;

namespace ZedZone.BetterReload
{
    internal enum SelectionStatus
    {
        Found,
        NoNonFullMagazines,
        NoCompatibleAmmo
    }

    internal sealed class SelectionResult
    {
        public SelectionStatus Status;
        public ItemData Magazine;
        public ItemAttr_Magazine MagAttr;
        public int CurrentAmmo;
        public int MaxAmmo;
        public int AvailableAmmo;
        public int Tier;
        public string MissingAmmoMagName;
    }

    /// <summary>
    /// 按“当前手持武器 -> 其它已装备武器 -> 其它兼容弹匣”三档优先级从携带容器（含多层套包）中挑选最合适的未满弹匣。
    /// </summary>
    internal static class MagazineSelector
    {
        internal static SelectionResult SelectBestMagazine(
            HumanCharacterController player,
            bool includeOtherMagazines,
            bool preferEmptiestFirst)
        {
            var result = new SelectionResult { Status = SelectionStatus.NoNonFullMagazines };
            if (player == null || player.characterData == null) return result;

            var itemMgr = ItemManager.instance;
            if (itemMgr == null) return result;

            var activeWeaponAttr = ResolveActiveWeaponAttr(player, itemMgr);
            var equippedWeaponAttrs = ResolveEquippedWeaponAttrs(player.characterData, itemMgr);

            var inventories = NestedAmmoHelper.CollectCarriedInventories(player.characterData);
            var seenMagazines = new HashSet<IntPtr>();
            var ammoCache = new Dictionary<int, int>();

            SelectionResult bestFillable = null;
            int bestMissingTier = int.MaxValue;
            string bestMissingName = null;

            for (int i = 0; i < inventories.Count; i++)
            {
                var items = inventories[i].itemList;
                if (items == null) continue;

                for (int j = 0; j < items.Count; j++)
                {
                    var item = items[j];
                    if (item == null) continue;

                    IntPtr ptr = NestedAmmoHelper.Key(item);
                    if (ptr == IntPtr.Zero || !seenMagazines.Add(ptr)) continue;

                    var attr = itemMgr.GetItemAttrById(item.itemId);
                    if (attr == null || attr.itemType != ItemType.Magazine) continue;

                    var magAttr = attr.TryCast<ItemAttr_Magazine>();
                    if (magAttr == null || magAttr.magazineSize <= 0 || magAttr.ammoId <= 0)
                        continue;

                    int loaded = ItemData.GetMagazineAmmoNumber(item);
                    if (loaded >= magAttr.magazineSize)
                        continue;

                    int tier = ResolveTier(magAttr, activeWeaponAttr, equippedWeaponAttrs, includeOtherMagazines);
                    if (tier < 0) continue;

                    if (!ammoCache.TryGetValue(magAttr.ammoId, out int availableAmmo))
                    {
                        availableAmmo = player.GetTotalAmmoNumberById(magAttr.ammoId, null);
                        ammoCache[magAttr.ammoId] = availableAmmo;
                    }

                    if (availableAmmo <= 0)
                    {
                        if (tier < bestMissingTier)
                        {
                            bestMissingTier = tier;
                            bestMissingName = SafeGetMagazineName(item, attr);
                        }
                        continue;
                    }

                    var candidate = new SelectionResult
                    {
                        Status = SelectionStatus.Found,
                        Magazine = item,
                        MagAttr = magAttr,
                        CurrentAmmo = loaded,
                        MaxAmmo = magAttr.magazineSize,
                        AvailableAmmo = availableAmmo,
                        Tier = tier
                    };

                    if (bestFillable == null || IsBetterCandidate(candidate, bestFillable, preferEmptiestFirst))
                    {
                        bestFillable = candidate;
                    }
                }
            }

            if (bestFillable != null)
                return bestFillable;

            if (!string.IsNullOrEmpty(bestMissingName))
            {
                result.Status = SelectionStatus.NoCompatibleAmmo;
                result.MissingAmmoMagName = bestMissingName;
            }

            return result;
        }

        private static ItemAttr_RangedWeapon ResolveActiveWeaponAttr(HumanCharacterController player, ItemManager itemMgr)
        {
            if (player.rangedWeapon != null && player.rangedWeapon.weaponAttr != null)
                return player.rangedWeapon.weaponAttr;

            var currentWeapon = player.characterData?.characterEquipmentData?.currentWeaponItemData;
            if (currentWeapon == null) return null;

            return itemMgr.GetItemAttrById(currentWeapon.itemId)?.TryCast<ItemAttr_RangedWeapon>();
        }

        private static List<ItemAttr_RangedWeapon> ResolveEquippedWeaponAttrs(CharacterData characterData, ItemManager itemMgr)
        {
            var list = new List<ItemAttr_RangedWeapon>();
            var weaponAry = characterData?.characterEquipmentData?.weaponItemDataAry;
            if (weaponAry == null) return list;

            for (int i = 0; i < weaponAry.Length; i++)
            {
                var weaponData = weaponAry[i];
                if (weaponData == null) continue;

                var weaponAttr = itemMgr.GetItemAttrById(weaponData.itemId)?.TryCast<ItemAttr_RangedWeapon>();
                if (weaponAttr != null)
                    list.Add(weaponAttr);
            }

            return list;
        }

        private static int ResolveTier(
            ItemAttr_Magazine magAttr,
            ItemAttr_RangedWeapon activeWeaponAttr,
            List<ItemAttr_RangedWeapon> equippedWeaponAttrs,
            bool includeOtherMagazines)
        {
            if (activeWeaponAttr != null && magAttr.IsMagazineFit(activeWeaponAttr))
                return 0;

            for (int i = 0; i < equippedWeaponAttrs.Count; i++)
            {
                if (magAttr.IsMagazineFit(equippedWeaponAttrs[i]))
                    return 1;
            }

            return includeOtherMagazines ? 2 : -1;
        }

        private static bool IsBetterCandidate(SelectionResult cand, SelectionResult best, bool preferEmptiestFirst)
        {
            if (cand.Tier != best.Tier)
                return cand.Tier < best.Tier;

            int candMissing = cand.MaxAmmo - cand.CurrentAmmo;
            int bestMissing = best.MaxAmmo - best.CurrentAmmo;

            if (preferEmptiestFirst)
            {
                if (cand.CurrentAmmo != best.CurrentAmmo)
                    return cand.CurrentAmmo < best.CurrentAmmo;
                if (candMissing != bestMissing)
                    return candMissing > bestMissing;
            }
            else
            {
                if (candMissing != bestMissing)
                    return candMissing < bestMissing;
                if (cand.CurrentAmmo != best.CurrentAmmo)
                    return cand.CurrentAmmo > best.CurrentAmmo;
            }

            return cand.MaxAmmo > best.MaxAmmo;
        }

        private static string SafeGetMagazineName(ItemData item, ItemAttr attr)
        {
            try
            {
                string name = item.GetItemName();
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch
            {
            }

            return attr?.ItemName ?? $"#{item.itemId}";
        }
    }
}
