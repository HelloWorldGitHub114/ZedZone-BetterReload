using System;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.BetterReload
{
    [HarmonyPatch(typeof(CharacterInput), nameof(CharacterInput.Update))]
    internal static class Patch_CharacterInput_Update
    {
        [HarmonyPrefix]
        private static void Prefix(CharacterInput __instance)
        {
            try
            {
                ReloadCoordinator.OnCharacterInputUpdatePrefix(__instance);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("input-update", ex);
            }
        }
    }

    [HarmonyPatch(typeof(GameInputManager), nameof(GameInputManager.GetButtonDown), new[] { typeof(string) })]
    internal static class Patch_GameInputManager_GetButtonDown
    {
        [HarmonyPrefix]
        private static bool Prefix(string buttonName, ref bool __result)
        {
            if (ReloadCoordinator.ShouldSuppressButton(buttonName))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameInputManager), nameof(GameInputManager.GetButton), new[] { typeof(string) })]
    internal static class Patch_GameInputManager_GetButton
    {
        [HarmonyPrefix]
        private static bool Prefix(string buttonName, ref bool __result)
        {
            if (ReloadCoordinator.ShouldSuppressButton(buttonName))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameInputManager), nameof(GameInputManager.GetButtonUp), new[] { typeof(string) })]
    internal static class Patch_GameInputManager_GetButtonUp
    {
        [HarmonyPrefix]
        private static bool Prefix(string buttonName, ref bool __result)
        {
            if (ReloadCoordinator.ShouldSuppressButton(buttonName))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.OnLoadMagazineFinished), new[] { typeof(CharacterActionData) })]
    internal static class Patch_CharacterActionManager_OnLoadMagazineFinished
    {
        [HarmonyPostfix]
        private static void Postfix(CharacterActionData m_characterActionData)
        {
            try
            {
                ReloadCoordinator.OnLoadMagazineFinishedPostfix(m_characterActionData);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("load-finished", ex);
            }
        }
    }

    [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.CancelPlayerAction))]
    internal static class Patch_CharacterActionManager_CancelPlayerAction
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            try
            {
                ReloadCoordinator.OnCancelPlayerActionPrefix();
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("cancel-player", ex);
            }
        }
    }

    [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.OnActionCancel), new[] { typeof(CharacterActionData) })]
    internal static class Patch_CharacterActionManager_OnActionCancel
    {
        [HarmonyPostfix]
        private static void Postfix(CharacterActionData m_characterActionData)
        {
            try
            {
                ReloadCoordinator.OnActionCanceled(m_characterActionData);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("action-cancel", ex);
            }
        }
    }

    [HarmonyPatch(typeof(BasicCharacterController), nameof(BasicCharacterController.GetAllItemListById), new[] { typeof(CharacterData), typeof(int) })]
    internal static class Patch_BasicCharacterController_GetAllItemListById_NestedAmmo
    {
        [HarmonyPostfix]
        private static void Postfix(
            CharacterData m_characterData,
            int m_itemID,
            ref Il2CppSystem.Collections.Generic.List<ItemData> __result)
        {
            if (!Plugin.EnableNestedAmmoFallback.Value) return;
            try
            {
                if (__result == null) return;
                int added = NestedAmmoHelper.AppendNestedAmmoItems(m_characterData, m_itemID, __result);
                if (added > 0)
                    Plugin.LogVerbose($"Nested ammo fallback added {added} stack(s) for itemId={m_itemID}.");
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("nested-ammo-list", ex);
            }
        }
    }

    [HarmonyPatch(typeof(BasicCharacterController), nameof(BasicCharacterController.RemoveItemFromCharacter), new[] { typeof(CharacterData), typeof(ItemData), typeof(bool) })]
    internal static class Patch_BasicCharacterController_RemoveItemFromCharacter_NestedAmmo
    {
        [HarmonyPostfix]
        private static void Postfix(
            CharacterData characterData,
            ItemData m_itemData,
            bool m_executeOnChange,
            ref bool __result)
        {
            if (__result || !Plugin.EnableNestedAmmoFallback.Value) return;
            try
            {
                if (NestedAmmoHelper.TryRemoveNestedItem(characterData, m_itemData, m_executeOnChange))
                {
                    __result = true;
                    Plugin.LogVerbose($"Nested ammo fallback removed itemId={m_itemData?.itemId}.");
                }
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("nested-ammo-remove", ex);
            }
        }
    }

    [HarmonyPatch(typeof(BasicCharacterController), nameof(BasicCharacterController.FindOwnerInventoryData), new[] { typeof(CharacterData), typeof(ItemData) })]
    internal static class Patch_BasicCharacterController_FindOwnerInventoryData_Nested
    {
        [HarmonyPostfix]
        private static void Postfix(CharacterData characterData, ItemData m_itemData, ref InventoryData __result)
        {
            if (__result != null || !Plugin.ReturnToNestedOriginalSlot.Value) return;
            try
            {
                var found = NestedAmmoHelper.FindOwnerInventoryRecursive(characterData, m_itemData);
                if (found != null)
                {
                    __result = found;
                    Plugin.LogVerbose($"Resolved nested owner inventory for itemId={m_itemData?.itemId}.");
                }
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("find-owner-inv", ex);
            }
        }
    }

    [HarmonyPatch(typeof(BasicCharacterController), nameof(BasicCharacterController.IsCharacterOwnedInventory), new[] { typeof(CharacterData), typeof(InventoryData) })]
    internal static class Patch_BasicCharacterController_IsCharacterOwnedInventory_Nested
    {
        [HarmonyPostfix]
        private static void Postfix(CharacterData m_characterData, InventoryData m_inventoryData, ref bool __result)
        {
            if (__result || !Plugin.ReturnToNestedOriginalSlot.Value) return;
            try
            {
                if (NestedAmmoHelper.IsCarriedInventory(m_characterData, m_inventoryData))
                {
                    __result = true;
                    Plugin.LogVerbose("Recognized nested carried inventory as character-owned for vacated-slot return.");
                }
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("is-owned-inv", ex);
            }
        }
    }

    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.OnMouseDown_Leven), new[] { typeof(int) })]
    internal static class Patch_HumanCharacterController_OnMouseDown_AutoReload
    {
        [HarmonyPrefix]
        private static void Prefix(HumanCharacterController __instance, int mouseIndex, out bool __state)
        {
            __state = false;
            try
            {
                __state = ReloadCoordinator.ShouldTriggerEmptyClickOnMouseDownPrefix(__instance, mouseIndex);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("mouse-down-prefix", ex);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(HumanCharacterController __instance, int mouseIndex, bool __state)
        {
            try
            {
                ReloadCoordinator.OnPlayerMouseDownPostfix(__instance, mouseIndex, __state);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("mouse-down-postfix", ex);
            }
        }
    }

    /// <summary>
    /// 修正右下角 HUD 备弹数量只统计散弹不计入备用弹匣内子弹的问题。
    /// </summary>
    [HarmonyPatch(typeof(RangedWeaponInfoPanel), nameof(RangedWeaponInfoPanel.RefreshInventoryBullet))]
    internal static class Patch_RangedWeaponInfoPanel_RefreshInventoryBullet
    {
        [HarmonyPostfix]
        private static void Postfix(RangedWeaponInfoPanel __instance)
        {
            if (Plugin.IncludeMagazineAmmoInHud == null || !Plugin.IncludeMagazineAmmoInHud.Value)
                return;

            try
            {
                if (__instance == null || __instance.weapon == null || __instance.gunAmmoText == null)
                    return;

                var weapon = __instance.weapon;
                var attr = weapon.weaponAttr;
                if (attr == null || weapon.character == null || weapon.weaponData == null)
                    return;

                // 跳过弓箭、弩、能量与液体武器（它们不走 gunAmmoText 弹匣体系）
                var wt = attr.weaponType;
                if (wt == RangedWeaponType.Bow ||
                    wt == RangedWeaponType.Crossbow ||
                    wt == RangedWeaponType.EnergyWeapon ||
                    wt == RangedWeaponType.LiquidWeapon)
                    return;

                int magAmmo = NestedAmmoHelper.CountCompatibleMagazineAmmo(weapon.character, attr);
                if (magAmmo <= 0)
                    return;

                int looseAmmo = BasicRangedWeapon.GetInventoryAmmoTotal(
                    weapon.character,
                    weapon.weaponData,
                    weapon.assignArrowId);

                var style = Plugin.HudMagazineAmmoStyle != null
                    ? Plugin.HudMagazineAmmoStyle.Value
                    : HudReserveAmmoStyle.Combined;

                string reserveText;
                if (style == HudReserveAmmoStyle.Split)
                {
                    reserveText = $"{looseAmmo}+{magAmmo}";
                    __instance.gunAmmoText.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
                else
                {
                    reserveText = (looseAmmo + magAmmo).ToString();
                }

                if (__instance.ShowLoadedAmmoInText())
                {
                    int loaded = BasicRangedWeapon.GetLoadedAmmo(weapon.weaponData);
                    __instance.gunAmmoText.text = $"{loaded}/{reserveText}";
                }
                else
                {
                    __instance.gunAmmoText.text = reserveText;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("hud-mag-ammo", ex);
            }
        }
    }
}
