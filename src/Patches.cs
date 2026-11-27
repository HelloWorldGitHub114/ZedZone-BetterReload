using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

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

    [HarmonyPatch(typeof(RangedWeaponInfoPanel), nameof(RangedWeaponInfoPanel.SyncBulletIconsAfterFire))]
    internal static class Patch_RangedWeaponInfoPanel_SyncBulletIconsAfterFire
    {
        [ThreadStatic]
        private static (int Depth, IntPtr Panel) _scope;

        internal static bool IsSyncing(RangedWeaponInfoPanel panel)
        {
            return _scope.Depth > 0 && _scope.Panel == NestedAmmoHelper.Key(panel);
        }

        [HarmonyPrefix]
        private static void Prefix(RangedWeaponInfoPanel __instance, out (int, IntPtr) __state)
        {
            __state = _scope;
            _scope = (_scope.Depth + 1, NestedAmmoHelper.Key(__instance));
        }

        [HarmonyPostfix]
        private static void Postfix((int, IntPtr) __state)
        {
            _scope = __state;
        }

        // 原方法或其他补丁抛异常时也退出开火范围，避免后续正常刷新误走缓存。
        [HarmonyFinalizer]
        private static void Finalizer((int, IntPtr) __state)
        {
            _scope = __state;
        }
    }

    [HarmonyPatch(typeof(RangedWeaponInfoPanel), nameof(RangedWeaponInfoPanel.Refresh))]
    internal static class Patch_RangedWeaponInfoPanel_Refresh
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Patch_RangedWeaponInfoPanel_RefreshInventoryBullet.Invalidate();
        }
    }

    [HarmonyPatch(typeof(RangedWeaponInfoPanel), nameof(RangedWeaponInfoPanel.OnReloadFinished))]
    internal static class Patch_RangedWeaponInfoPanel_OnReloadFinished
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Patch_RangedWeaponInfoPanel_RefreshInventoryBullet.Invalidate();
        }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.ReloadItemRegistry))]
    internal static class Patch_GameController_ReloadItemRegistry
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            ClearCaches();
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            ClearCaches();
        }

        private static void ClearCaches()
        {
            // 热重载会重建物品定义，也可能继续使用同一个 ItemManager。
            NestedAmmoHelper.ClearMetadataCache();
            Patch_RangedWeaponInfoPanel_RefreshInventoryBullet.Invalidate();
        }
    }

    [HarmonyPatch(typeof(RangedWeaponInfoPanel), nameof(RangedWeaponInfoPanel.RefreshInventoryBullet))]
    internal static class Patch_RangedWeaponInfoPanel_RefreshInventoryBullet
    {
        private const float MaxReserveAge = 1f;

        // 指针只用于比较当前对象身份，缓存不持有 IL2CPP 对象。
        private readonly record struct ReserveKey(
            IntPtr Panel, IntPtr Text, IntPtr Character, IntPtr CharacterData, IntPtr Weapon,
            IntPtr WeaponData, IntPtr WeaponAttr, IntPtr ItemManager, bool IncludeMagAmmo,
            bool NestedAmmoFallback, HudReserveAmmoStyle Style);

        private static ReserveKey _reserveKey;
        private static float _reserveTime;
        private static string _reserveText;
        private static string _renderedText;
        private static int _overflowTextId;
        private static HorizontalWrapMode _originalOverflow;

        internal static void Invalidate()
        {
            _reserveKey = default;
            _reserveText = null;
            _renderedText = null;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(RangedWeaponInfoPanel __instance, bool __runOriginal)
        {
            if (!__runOriginal || _reserveText == null ||
                !Patch_RangedWeaponInfoPanel_SyncBulletIconsAfterFire.IsSyncing(__instance))
                return true;

            try
            {
                float age = Time.unscaledTime - _reserveTime;
                if (age < 0f || age > MaxReserveAge ||
                    !TryGetState(__instance, out var state) || state.Key != _reserveKey)
                    return true;

                string current = state.Text.text;
                // 其他模组可能接管了这段文本，交给正常刷新链路处理。
                if (current != _renderedText)
                {
                    Invalidate();
                    return true;
                }

                // 开火只改枪内弹量，背包备弹由库存事件和原版轮询独立刷新。
                string desired = __instance.ShowLoadedAmmoInText()
                    ? $"{BasicRangedWeapon.GetLoadedAmmo(state.WeaponData)}/{_reserveText}"
                    : _reserveText;
                if (current != desired)
                    state.Text.text = desired;
                _renderedText = desired;
                return false;
            }
            catch (Exception ex)
            {
                Invalidate();
                Plugin.LogOnce("hud-fire-ammo", ex);
                return true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void Postfix(RangedWeaponInfoPanel __instance, bool __runOriginal)
        {
            // HarmonyX 跳过原方法时仍会执行后缀，此时文本已经是最终值。
            if (!__runOriginal) return;
            Invalidate();

            try
            {
                if (!TryGetState(__instance, out var state))
                {
                    if (__instance != null)
                        RestoreOverflow(__instance.gunAmmoText);
                    return;
                }

                // 先读取原版刚写入的散弹数，省去重复的库存查询，并给其他 HUD 后缀留出覆盖机会。
                var text = state.Text;
                string vanilla = text.text;
                if (!TryParseLooseAmmo(vanilla, out int separator, out int looseAmmo))
                {
                    RestoreOverflow(text);
                    return;
                }

                int magAmmo = state.Key.IncludeMagAmmo && state.Attr.feedingType == ItemAttr_RangedWeapon.FeedingType.Magazine
                    ? NestedAmmoHelper.CountCompatibleMagazineAmmo(state.Character, state.Attr)
                    : 0;
                bool split = magAmmo > 0 && state.Key.Style == HudReserveAmmoStyle.Split;
                UpdateOverflow(text, split);

                string reserve;
                string desired = vanilla;
                if (magAmmo > 0)
                {
                    reserve = split ? $"{looseAmmo}+{magAmmo}" : ((long)looseAmmo + magAmmo).ToString();
                    desired = separator < 0 ? reserve : string.Concat(vanilla.AsSpan(0, separator + 1), reserve.AsSpan());
                    if (vanilla != desired)
                        text.text = desired;
                }
                else
                {
                    reserve = separator < 0 ? vanilla : vanilla.Substring(separator + 1);
                }

                _reserveKey = state.Key;
                _reserveTime = Time.unscaledTime;
                _reserveText = reserve;
                _renderedText = desired;
            }
            catch (Exception ex)
            {
                Plugin.LogOnce("hud-mag-ammo", ex);
            }
        }

        private static bool TryGetState(
            RangedWeaponInfoPanel panel,
            out (Text Text, BasicCharacterController Character, ItemData WeaponData, ItemAttr_RangedWeapon Attr, ReserveKey Key) state)
        {
            state = default;
            if (panel == null) return false;
            var text = panel.gunAmmoText;
            var weapon = panel.weapon;
            if (text == null || weapon == null) return false;

            var attr = weapon.weaponAttr;
            if (attr == null ||
                (attr.weaponType is not (RangedWeaponType.ShotGun or RangedWeaponType.Pistol or
                    RangedWeaponType.Rifle or RangedWeaponType.Revolver)) ||
                (attr.feedingType is not (ItemAttr_RangedWeapon.FeedingType.Magazine or
                    ItemAttr_RangedWeapon.FeedingType.BuiltIn)))
                return false;

            var character = weapon.character;
            var weaponData = weapon.weaponData;
            if (character == null || weaponData == null) return false;
            var characterData = character.characterData;
            if (characterData == null || characterData.deadFlag || character.netDeadFlag) return false;

            var game = GameController.instance;
            if (game != null && game.weaponTestMode) return false;

            var key = new ReserveKey(panel.Pointer, text.Pointer, character.Pointer, characterData.Pointer,
                weapon.Pointer, weaponData.Pointer, attr.Pointer, NestedAmmoHelper.Key(ItemManager.instance),
                Plugin.IncludeMagazineAmmoInHud.Value, Plugin.EnableNestedAmmoFallback.Value, Plugin.HudMagazineAmmoStyle.Value);
            state = (text, character, weaponData, attr, key);
            return true;
        }

        private static bool TryParseLooseAmmo(string text, out int separator, out int looseAmmo)
        {
            separator = text?.IndexOf('/') ?? -1;
            looseAmmo = 0;
            if (string.IsNullOrEmpty(text)) return false;
            if (separator >= 0 && !int.TryParse(text.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out _))
                return false;

            return int.TryParse(text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out looseAmmo);
        }

        private static void UpdateOverflow(Text text, bool split)
        {
            if (!split)
            {
                RestoreOverflow(text);
                return;
            }

            var current = text.horizontalOverflow;
            if (current == HorizontalWrapMode.Overflow) return;

            // 只记录本模组实际改动的组件，切回合并时恢复它原有的换行方式。
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _overflowTextId = text.GetInstanceID();
            _originalOverflow = current;
        }

        private static void RestoreOverflow(Text text)
        {
            if (_overflowTextId == 0 || text == null || _overflowTextId != text.GetInstanceID())
                return;

            if (text.horizontalOverflow == HorizontalWrapMode.Overflow)
                text.horizontalOverflow = _originalOverflow;
            _overflowTextId = 0;
        }
    }
}
