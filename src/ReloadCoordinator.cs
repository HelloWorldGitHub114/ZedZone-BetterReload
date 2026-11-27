using System;
using UnityEngine;

namespace ZedZone.BetterReload
{
    /// <summary>
    /// 协调快捷键检测、原版按键隔离、状态安全校验、压弹触发与自动连压队列。
    /// </summary>
    internal static class ReloadCoordinator
    {
        private const float ContinueDelaySeconds = 0.08f;

        private static bool _ourSessionActive;
        private static bool _pendingContinue;
        private static float _pendingContinueAt;
        private static IntPtr _lastPlayerPtr;

        private static bool _suppressVanillaKey;
        private static KeyCode _suppressedKey;
        private static int _suppressStartFrame;

        private static IntPtr _trackedWeaponPtr;
        private static bool _wasNonEmpty;
        private static bool _pendingAutoReload;
        private static IntPtr _pendingAutoReloadWeaponPtr;
        private static float _pendingAutoReloadAt;

        internal static void ResetSession()
        {
            _ourSessionActive = false;
            _pendingContinue = false;
            _pendingContinueAt = 0f;
        }

        private static void ResetAutoReload()
        {
            _pendingAutoReload = false;
            _pendingAutoReloadWeaponPtr = IntPtr.Zero;
            _pendingAutoReloadAt = 0f;
        }

        private static void ResetAll()
        {
            Patch_RangedWeaponInfoPanel_RefreshInventoryBullet.Invalidate();
            ResetSession();
            ResetAutoReload();
            _trackedWeaponPtr = IntPtr.Zero;
            _wasNonEmpty = false;
        }

        internal static bool ShouldSuppressButton(string buttonName)
        {
            if (!_suppressVanillaKey || string.IsNullOrEmpty(buttonName))
                return false;

            try
            {
                return GameInputManager.GetKeyMap(buttonName, false) == _suppressedKey;
            }
            catch
            {
                return false;
            }
        }

        internal static void OnCharacterInputUpdatePrefix(CharacterInput input)
        {
            if (_suppressVanillaKey && Time.frameCount > _suppressStartFrame && !IsKeyHeldSafe(_suppressedKey))
            {
                _suppressVanillaKey = false;
            }

            if (input == null) return;

            var gameCtrl = GameController.instance;
            if (gameCtrl == null)
            {
                if (_lastPlayerPtr != IntPtr.Zero)
                {
                    ResetAll();
                    _lastPlayerPtr = IntPtr.Zero;
                }
                return;
            }

            var player = gameCtrl.playerCharacter ?? input.characterController;
            if (player == null || !player.isPlayer)
            {
                if (_lastPlayerPtr != IntPtr.Zero)
                {
                    ResetAll();
                    _lastPlayerPtr = IntPtr.Zero;
                }
                return;
            }

            if (player.Pointer != _lastPlayerPtr)
            {
                ResetAll();
                _lastPlayerPtr = player.Pointer;
            }

            TickWeaponAutoReload(input, player, gameCtrl);

            // 延后到动作管理器完成清理后的下一轮输入更新再续压下一个弹匣，避免状态机重入冲突
            if (_pendingContinue && Time.unscaledTime >= _pendingContinueAt)
            {
                _pendingContinue = false;
                if (CanStartReload(input, player, isAutoContinue: true))
                {
                    TryStartReload(player, isAutoContinue: true);
                }
                else
                {
                    _ourSessionActive = false;
                }
            }

            if (!GameInputManager.inputAllowed ||
                input.stopAllInput ||
                input.anyBlockingPanelOpen ||
                input.isInputingText ||
                Time.timeScale <= 0f ||
                gameCtrl.isSoftPaused)
            {
                return;
            }

            KeyCode trigger = Plugin.TriggerKey.Value;
            KeyCode modifier = Plugin.ModifierKey.Value;
            if (trigger == KeyCode.None) return;

            if (IsModifierHeld(modifier) && GameInputManager.GetKeyDown(trigger))
            {
                _suppressVanillaKey = true;
                _suppressedKey = trigger;
                _suppressStartFrame = Time.frameCount;

                HandleHotkeyPressed(input, player);
            }
        }

        private static bool IsKeyHeldSafe(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            try
            {
                return GameInputManager.GetKey(key);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsModifierHeld(KeyCode modifier)
        {
            if (modifier == KeyCode.None) return true;
            if (modifier == KeyCode.LeftAlt)
                return GameInputManager.GetKey(KeyCode.LeftAlt) || GameInputManager.GetKey(KeyCode.RightAlt);
            if (modifier == KeyCode.LeftControl)
                return GameInputManager.GetKey(KeyCode.LeftControl) || GameInputManager.GetKey(KeyCode.RightControl);
            if (modifier == KeyCode.LeftShift)
                return GameInputManager.GetKey(KeyCode.LeftShift) || GameInputManager.GetKey(KeyCode.RightShift);

            return GameInputManager.GetKey(modifier);
        }

        private static void HandleHotkeyPressed(CharacterInput input, HumanCharacterController player)
        {
            var actionMgr = CharacterActionManager.instance;
            if (actionMgr == null) return;

            if (actionMgr.HasPlayerAction())
                return;

            if (!CanStartReload(input, player, isAutoContinue: false))
                return;

            ResetAutoReload();
            TryStartReload(player, isAutoContinue: false);
        }

        internal static bool IsWeaponEmpty(BasicRangedWeapon weapon)
        {
            if (weapon == null || weapon.weaponData == null || weapon.weaponAttr == null)
                return false;

            if (weapon.weaponAttr.weaponType == RangedWeaponType.Bow)
                return false;

            var feeding = weapon.weaponAttr.feedingType;
            if (feeding == ItemAttr_RangedWeapon.FeedingType.Magazine ||
                feeding == ItemAttr_RangedWeapon.FeedingType.BuiltIn)
            {
                return BasicRangedWeapon.GetLoadedAmmo(weapon.weaponData) <= 0;
            }

            if (feeding == ItemAttr_RangedWeapon.FeedingType.LiquidContainer)
            {
                return weapon.weaponData.rangedWeaponLiquidVolume <= 0.001f;
            }

            return false;
        }

        internal static bool ShouldTriggerEmptyClickOnMouseDownPrefix(HumanCharacterController player, int mouseIndex)
        {
            if (mouseIndex != 0 || !Plugin.AutoReloadOnEmpty.Value)
                return false;

            if (player == null || !player.isPlayer)
                return false;

            if (player.holsterWeapon || player.reloadingMagazineFlag || player.isDriving || player.isBuildingObject)
                return false;

            var weapon = player.rangedWeapon;
            if (weapon == null || weapon.isReloading || weapon.isAirCharging || weapon.isMalfunction)
                return false;

            return IsWeaponEmpty(weapon);
        }

        internal static void OnPlayerMouseDownPostfix(HumanCharacterController player, int mouseIndex, bool wasAlreadyEmpty)
        {
            if (!wasAlreadyEmpty || mouseIndex != 0 || !Plugin.AutoReloadOnEmpty.Value)
                return;

            if (player == null || !player.isPlayer || player.holsterWeapon || player.reloadingMagazineFlag)
                return;

            var weapon = player.rangedWeapon;
            if (weapon == null || weapon.isReloading || weapon.isAirCharging || weapon.isMalfunction)
                return;

            if (!IsWeaponEmpty(weapon))
                return;

            _pendingAutoReload = true;
            _pendingAutoReloadWeaponPtr = weapon.Pointer;
            _pendingAutoReloadAt = Time.unscaledTime + 0.05f;
        }

        private static void TickWeaponAutoReload(
            CharacterInput input,
            HumanCharacterController player,
            GameController gameCtrl)
        {
            var weapon = player.rangedWeapon;
            if (weapon == null || weapon.weaponData == null || weapon.weaponAttr == null)
            {
                _trackedWeaponPtr = IntPtr.Zero;
                _wasNonEmpty = false;
                ResetAutoReload();
                return;
            }

            bool isEmpty = IsWeaponEmpty(weapon);
            if (weapon.Pointer != _trackedWeaponPtr)
            {
                // 切枪时只同步当前弹量基线，避免把本来就空的枪误判为刚打空
                _trackedWeaponPtr = weapon.Pointer;
                _wasNonEmpty = !isEmpty;
                ResetAutoReload();
                return;
            }

            bool gameplayActive =
                GameInputManager.inputAllowed &&
                !input.stopAllInput &&
                !input.anyBlockingPanelOpen &&
                !input.isInputingText &&
                Time.timeScale > 0f &&
                !gameCtrl.isSoftPaused &&
                !player.holsterWeapon;

            if (!gameplayActive ||
                weapon.isReloading ||
                weapon.isAirCharging ||
                weapon.isMalfunction ||
                player.reloadingMagazineFlag)
            {
                // 打开背包卸弹、卡壳或收枪期间同步状态并取消待触发自动换弹
                _wasNonEmpty = !isEmpty;
                ResetAutoReload();
                return;
            }

            if (_wasNonEmpty && isEmpty)
            {
                _wasNonEmpty = false;
                if (Plugin.AutoReloadOnEmpty.Value)
                {
                    float delay = Mathf.Clamp(Plugin.AutoReloadDelay.Value, 0f, 2f);
                    _pendingAutoReload = true;
                    _pendingAutoReloadWeaponPtr = weapon.Pointer;
                    _pendingAutoReloadAt = Time.unscaledTime + delay;
                }
            }
            else if (!isEmpty)
            {
                _wasNonEmpty = true;
                if (_pendingAutoReload)
                    ResetAutoReload();
            }

            if (_pendingAutoReload && Time.unscaledTime >= _pendingAutoReloadAt)
            {
                bool sameWeapon = weapon.Pointer == _pendingAutoReloadWeaponPtr;
                ResetAutoReload();

                if (sameWeapon && isEmpty && CanStartWeaponAutoReload(input, player, weapon))
                {
                    bool started = weapon.Reload();
                    Plugin.LogVerbose($"Auto-reload triggered on empty weapon '{weapon.weaponData.GetItemName()}', started={started}.");
                }
            }
        }

        private static bool IsPlayerStateReady(CharacterInput input, HumanCharacterController player, bool checkBlockingUi)
        {
            if (player == null || !player.isPlayer || player.characterData == null)
                return false;

            if (player.deadFlag || player.characterData.deadFlag || player.netDeadFlag)
                return false;

            if (player.netDownedFlag || player.impactKnockdownActive || player.IsNetImpactDowned)
                return false;

            if (player.isDriving || player.drivingVehicle != null)
                return false;

            if (player.isSleeping || player.isFishing || player.isBuildingObject)
                return false;

            var actionMgr = CharacterActionManager.instance;
            if (actionMgr == null || actionMgr.HasPlayerAction())
                return false;

            if (Time.timeScale <= 0f)
                return false;

            var gameCtrl = GameController.instance;
            if (gameCtrl != null && gameCtrl.isSoftPaused)
                return false;

            if (checkBlockingUi && input != null)
            {
                if (input.stopAllInput || input.anyBlockingPanelOpen || input.isInputingText)
                    return false;
            }

            return true;
        }

        private static bool CanStartWeaponAutoReload(
            CharacterInput input,
            HumanCharacterController player,
            BasicRangedWeapon weapon)
        {
            if (!IsPlayerStateReady(input, player, checkBlockingUi: true))
                return false;

            if (player.holsterWeapon || player.reloadingMagazineFlag)
                return false;

            if (weapon == null || weapon.isReloading || weapon.isAirCharging || weapon.isMalfunction)
                return false;

            // 确认确实有可替换弹匣或可用散弹，避免缺弹时反复弹出原版无弹提示
            return weapon.WouldReloadNow();
        }

        private static bool CanStartReload(CharacterInput input, HumanCharacterController player, bool isAutoContinue)
        {
            if (!IsPlayerStateReady(input, player, checkBlockingUi: isAutoContinue))
                return false;

            if (player.rangedWeapon != null && (player.rangedWeapon.isReloading || player.rangedWeapon.isAirCharging))
                return false;

            return true;
        }

        private static void TryStartReload(HumanCharacterController player, bool isAutoContinue)
        {
            var sel = MagazineSelector.SelectBestMagazine(
                player,
                Plugin.IncludeOtherMagazines.Value,
                Plugin.PreferEmptiestFirst.Value);

            if (sel.Status == SelectionStatus.Found)
            {
                var actionMgr = CharacterActionManager.instance;
                if (actionMgr == null)
                {
                    ResetSession();
                    return;
                }

                _ourSessionActive = true;
                _pendingContinue = false;

                actionMgr.ReloadMagazineAction(player, sel.Magazine, null);

                if (!actionMgr.HasPlayerAction() && !player.reloadingMagazineFlag)
                {
                    _ourSessionActive = false;
                    return;
                }

                Plugin.LogVerbose(
                    $"Started refilling '{sel.Magazine.GetItemName()}' ({sel.CurrentAmmo}/{sel.MaxAmmo}), tier={sel.Tier}, reserveAmmo={sel.AvailableAmmo}.");
                return;
            }

            _ourSessionActive = false;

            if (isAutoContinue) return;

            if (sel.Status == SelectionStatus.NoCompatibleAmmo)
            {
                Plugin.Notify(
                    $"没有可用子弹装填 {sel.MissingAmmoMagName}",
                    $"No compatible ammo for {sel.MissingAmmoMagName}",
                    false);
            }
            else
            {
                Plugin.Notify(
                    "没有需要装填的弹匣",
                    "No magazines need refilling",
                    false);
            }
        }

        internal static void OnLoadMagazineFinishedPostfix(CharacterActionData cad)
        {
            var character = cad?.character;
            if (character == null || !character.isPlayer) return;

            Patch_RangedWeaponInfoPanel_RefreshInventoryBullet.Invalidate();
            NestedAmmoHelper.CleanEmptyAmmoInAllCarried(character.characterData);

            if (_ourSessionActive && Plugin.AutoContinue.Value)
            {
                _pendingContinue = true;
                _pendingContinueAt = Time.unscaledTime + ContinueDelaySeconds;
            }
            else
            {
                _ourSessionActive = false;
            }

            // 散弹转入弹匣后立即重算，无论动作是快捷键发起还是手动发起。
            var panel = RangedWeaponInfoPanel._instance;
            if (panel != null && panel.isActiveAndEnabled)
                panel.RefreshInventoryBullet();
        }

        internal static void OnCancelPlayerActionPrefix()
        {
            ResetSession();
        }

        internal static void OnActionCanceled(CharacterActionData cad)
        {
            if (cad == null || cad.character == null || cad.character.isPlayer)
            {
                ResetSession();
            }
        }
    }
}
