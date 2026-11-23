using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.BetterReload
{
    public enum HudReserveAmmoStyle
    {
        Combined,
        Split
    }

    /// <summary>
    /// 换弹增强插件入口：提供快捷键一键压弹、打空自动换弹、多层套包弹匣原位归回与 HUD 弹匣备弹统计。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGuid = "zedzone.betterreload";
        public const string PluginName = "Better Reload";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Logs;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowNotifications;
        internal static ConfigEntry<bool> VerboseLog;

        internal static ConfigEntry<KeyCode> TriggerKey;
        internal static ConfigEntry<KeyCode> ModifierKey;

        internal static ConfigEntry<bool> AutoContinue;
        internal static ConfigEntry<bool> IncludeOtherMagazines;
        internal static ConfigEntry<bool> PreferEmptiestFirst;
        internal static ConfigEntry<bool> EnableNestedAmmoFallback;

        internal static ConfigEntry<bool> AutoReloadOnEmpty;
        internal static ConfigEntry<float> AutoReloadDelay;

        internal static ConfigEntry<bool> ReturnToNestedOriginalSlot;

        internal static ConfigEntry<bool> IncludeMagazineAmmoInHud;
        internal static ConfigEntry<HudReserveAmmoStyle> HudMagazineAmmoStyle;

        private static readonly HashSet<string> LoggedErrorTags = new HashSet<string>(StringComparer.Ordinal);

        public override void Load()
        {
            Logs = Log;

            Enabled = Config.Bind("General", "Enabled", true,
                "启用换弹增强模组。");
            ShowNotifications = Config.Bind("General", "ShowNotifications", true,
                "无可用弹匣或缺少对应子弹时在右下角显示系统提示。");
            VerboseLog = Config.Bind("General", "VerboseLog", false,
                "输出详细调试日志到 BepInEx\\LogOutput.log。");

            TriggerKey = Config.Bind("Hotkey", "TriggerKey", KeyCode.R,
                "触发一键压弹快捷键（支持键盘按键与鼠标侧键如 Mouse3、Mouse4）。");
            ModifierKey = Config.Bind("Hotkey", "ModifierKey", KeyCode.LeftAlt,
                "组合修饰键（如 LeftAlt、LeftControl、LeftShift；设为 None 可直接单键触发）。");

            AutoContinue = Config.Bind("Behavior", "AutoContinue", true,
                "当前弹匣压满后自动继续装填下一个未满弹匣。");
            IncludeOtherMagazines = Config.Bind("Behavior", "IncludeOtherMagazines", true,
                "当前及已装备枪械没有未满弹匣时，继续装填背包内其它任意型号的未满弹匣。");
            PreferEmptiestFirst = Config.Bind("Behavior", "PreferEmptiestFirst", true,
                "同优先级下优先装填剩余子弹最少的弹匣；关闭则优先补满最接近满弹的弹匣。");
            EnableNestedAmmoFallback = Config.Bind("Behavior", "EnableNestedAmmoFallback", true,
                "启用套包弹药兜底（单独安装本模组时也能从多层子背包取弹并清理空堆，与《更好的套包》自动去重兼容）。");

            AutoReloadOnEmpty = Config.Bind("AutoReload", "AutoReloadOnEmpty", true,
                "打空最后一发或空仓点击开火时自动触发换弹（跳过弓箭与卡壳状态）。");
            AutoReloadDelay = Config.Bind("AutoReload", "AutoReloadDelay", 0.15f,
                "打空最后一发后延迟多少秒开始自动换弹（秒，范围 0 ~ 2）。");

            ReturnToNestedOriginalSlot = Config.Bind("MagazineReturn", "ReturnToNestedOriginalSlot", true,
                "换弹时让多层套包内的弹匣/电池也能优先放回被换走物品的原容器与原格位，放不下时自动回退原版规则。");

            IncludeMagazineAmmoInHud = Config.Bind("HudDisplay", "IncludeMagazineAmmoInHud", true,
                "在右下角武器 HUD 备弹数量中计入随身兼容弹匣（含套包内弹匣）已装填的子弹数。");
            HudMagazineAmmoStyle = Config.Bind("HudDisplay", "HudMagazineAmmoStyle", HudReserveAmmoStyle.Combined,
                "HUD 备弹显示格式：Combined 合并显示总备弹（如 150 或 30/150）；Split 分开显示散弹+弹匣内子弹（如 60+90 或 30/60+90）。");

            if (!Enabled.Value)
            {
                Log.LogInfo($"{PluginName} is disabled in config.");
                return;
            }

            var harmony = new Harmony(PluginGuid);
            int applied = 0;
            int skipped = 0;

            Type[] patchTypes =
            {
                typeof(Patch_CharacterInput_Update),
                typeof(Patch_GameInputManager_GetButtonDown),
                typeof(Patch_GameInputManager_GetButton),
                typeof(Patch_GameInputManager_GetButtonUp),
                typeof(Patch_CharacterActionManager_OnLoadMagazineFinished),
                typeof(Patch_CharacterActionManager_CancelPlayerAction),
                typeof(Patch_CharacterActionManager_OnActionCancel),
                typeof(Patch_BasicCharacterController_GetAllItemListById_NestedAmmo),
                typeof(Patch_BasicCharacterController_RemoveItemFromCharacter_NestedAmmo),
                typeof(Patch_BasicCharacterController_FindOwnerInventoryData_Nested),
                typeof(Patch_BasicCharacterController_IsCharacterOwnedInventory_Nested),
                typeof(Patch_HumanCharacterController_OnMouseDown_AutoReload),
                typeof(Patch_RangedWeaponInfoPanel_RefreshInventoryBullet),
            };

            foreach (var patchType in patchTypes)
            {
                if (PatchOne(harmony, patchType))
                    applied++;
                else
                    skipped++;
            }

            int hooked = harmony.GetPatchedMethods().Count();
            Log.LogInfo($"{PluginName} {PluginVersion}: {applied} patch group(s) applied, {skipped} skipped, {hooked} method(s) hooked.");
        }

        private static bool PatchOne(Harmony harmony, Type patchType)
        {
            try
            {
                harmony.PatchAll(patchType);
                return true;
            }
            catch (Exception ex)
            {
                Logs?.LogWarning($"Patch {patchType.Name} skipped: {ex.Message}");
                return false;
            }
        }

        internal static bool IsChinese()
        {
            try
            {
                var settings = GameController.instance?.gameSettings;
                if (settings != null)
                    return settings.gameLanguage == GameLanguage.SimplifiedChinese;
            }
            catch
            {
                // 游戏尚未进入主流程时回退到系统界面语言判定
            }

            return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        internal static void Notify(string zh, string en, bool playSound = false)
        {
            if (!ShowNotifications.Value) return;
            try
            {
                if (SystemNotificationPanel.instance == null) return;
                SystemNotificationPanel.ShowNotificationSafe(IsChinese() ? zh : en, playSound);
            }
            catch (Exception ex)
            {
                LogOnce("notify", ex);
            }
        }

        internal static void LogVerbose(string message)
        {
            if (VerboseLog != null && VerboseLog.Value)
                Logs?.LogInfo("[Verbose] " + message);
        }

        internal static void LogOnce(string tag, Exception ex)
        {
            if (!LoggedErrorTags.Add(tag)) return;
            Logs?.LogWarning($"[{tag}] {ex.GetType().Name}: {ex.Message}");
        }
    }
}
