# Better Reload / 更好的换弹（一键压弹 · 自动换弹 · 弹匣归位）

围绕《隔离区（ZED ZONE）》枪械换弹与弹匣整理痛点打造的一体化换弹增强模组，全部功能均复用游戏原生动作与背包机制，可在配置文件中各自独立开关，且属于纯客户端操作模组，加入未安装本模组的房主世界也可正常使用。

An all-in-one reload QoL suite for ZED ZONE featuring hotkey magazine refilling without opening inventory, automatic reload on empty, and nested-bag vacated-slot magazine return. Every feature is independently configurable and 100% client-side safe for multiplayer.

---

## 功能特性 / Features

### 1. 快捷键一键压弹 / Quick Magazine Refill (`Alt + R`)
- **不开背包直接压弹**：按下快捷键直接挑选身上（含多层套包）最合适的未满弹匣，触发原版压弹读条、动作动画、音效与技能加成；同时屏蔽组合键对原版 `R` 键换弹/滚轮的误触发。
- **三档智能选匣**：
  1. 优先装填适配**当前手持枪械**的未满弹匣；
  2. 其次装填适配**武器栏其它已装备枪械**的未满弹匣；
  3. 最后装填背包内**其它任意型号**的未满弹匣（可在配置中关闭）。
- **自动连压与原生打断**：默认开启 `AutoContinue`，一个弹匣压满后自动无缝接续下一个未满弹匣；压弹途中按原版取消键（`F`）中断动作时也会自动停止连压。
- **多层套包全兼容**：递归扫描主背包、穿戴装备容器与多层子背包内的弹匣和子弹，通过对象指针去重，推荐搭配同系列 [《更好的套包 / Better Nested Bags》](https://steamcommunity.com/sharedfiles/filedetails/?id=3843482219) 无缝使用；单独安装本模组时也自带弹药取用与空堆清理兜底。
- **双语轻量提示**：缺对应子弹或无未满弹匣时，按游戏当前语言显示简短右下角提示。

### 2. 打空自动换弹 / Auto-Reload on Empty
- **打空与空仓点击自动换弹**：弹匣式/内置弹仓/液体武器射出最后一发打空后（默认短暂延迟 `0.15s`），或在空仓状态下点击左键开火时，若身上存在可用弹匣或备弹且处于正常持枪战斗状态，自动触发原版换弹流程。
- **安全护栏**：自动跳过弓箭与卡壳状态（`isMalfunction`），切换空枪、打开背包卸弹或背包内完全没有可用弹药/弹匣时不会误触发也不会刷屏提示。

### 3. 套包弹匣原位归回 / Nested Vacated-Slot Return
- **修补原版套包识别盲区**：游戏原版换弹时已有“优先将换出的旧弹匣放回新弹匣腾出的原格位，放不下再退回原容器或通用背包”的机制，但角色容器归属校验未识别嵌套子背包（弹匣袋/套包里的包），导致子背包里的弹匣每次换弹都会被扔到外层大背包里把包搞乱。
- **开启后效果**：多层套包中的弹匣或电池在换弹后，会按“原格位 → 旋转后原格位 → 同子背包其它空位 → 其它背包兜底”的规则优先回到原处（仅当旧弹匣尺寸更大且周围塞不下时才退回外层）。

### 4. 右下角 HUD 备弹计入弹匣内子弹 / HUD Reserve Ammo Includes Spare Magazines (v1.1.0)
- **修复备弹误报 0 发**：游戏原版右下角 HUD 备弹数字只统计背包里的散装子弹，不统计随身备用弹匣里已经压好的子弹（经常出现兜里揣着好几个满弹匣、右下角却显示 `0` 备弹的误导）。
- **智能兼容统计**：开启后自动递归统计随身（含多层套包）所有适配当前枪械的备用弹匣内子弹数，支持在配置中切换两种展示格式：
  - `Combined`（默认）：合并显示总备弹（例如 `150` 或 `30/150`），简洁贴合原版排版；
  - `Split`：分开显示散弹与弹匣内子弹（例如 `60+90` 或 `30/60+90`）。

---

## 前置依赖 / Requirements

需要先安装代码模组加载器：

- **ZED ZONE Code Mod Loader (`ZedModsLoader`)**
  https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258

---

## 默认操作与配置 / Controls & Config

- **一键压弹默认快捷键**：`LeftAlt + R`（同时兼容 `RightAlt + R`）

首次启动游戏后会自动生成配置文件：

```
BepInEx\config\zedzone.betterreload.cfg
```

| 节 / Section | 键 / Key | 默认值 / Default | 说明 / Description |
|---|---|---|---|
| `General` | `Enabled` | `true` | 总开关 |
| `General` | `ShowNotifications` | `true` | 无可装填弹匣或缺少子弹时显示系统提示 |
| `General` | `VerboseLog` | `false` | 输出详细调试日志到 `BepInEx\LogOutput.log` |
| `Hotkey` | `TriggerKey` | `R` | 一键压弹主触发键（支持 `T`、`K` 或鼠标侧键 `Mouse3`、`Mouse4` 等 Unity `KeyCode`） |
| `Hotkey` | `ModifierKey` | `LeftAlt` | 修饰键（设为 `None` 可改为纯单键/纯侧键触发） |
| `Behavior` | `AutoContinue` | `true` | 压满当前弹匣后自动继续压下一个未满弹匣 |
| `Behavior` | `IncludeOtherMagazines` | `true` | 手持与装备栏枪械弹匣都满后，继续装填背包内其它型号弹匣 |
| `Behavior` | `PreferEmptiestFirst` | `true` | 同优先级下优先压剩余子弹最少的弹匣；`false` 则优先补最接近满的弹匣 |
| `Behavior` | `EnableNestedAmmoFallback` | `true` | 内置多层子背包弹药查找、扣除与空堆清理兜底 |
| `AutoReload` | `AutoReloadOnEmpty` | `true` | 打空最后一发或空仓点击开火时自动触发换弹 |
| `AutoReload` | `AutoReloadDelay` | `0.15` | 打空最后一发后延迟几秒开始自动换弹（`0 ~ 2` 秒） |
| `MagazineReturn` | `ReturnToNestedOriginalSlot` | `true` | 换弹时让多层套包里的弹匣/电池也能优先放回原容器与原格位 |
| `HudDisplay` | `IncludeMagazineAmmoInHud` | `true` | 右下角 HUD 备弹数量中计入随身兼容备用弹匣内的子弹数 |
| `HudDisplay` | `HudMagazineAmmoStyle` | `Combined` | HUD 备弹显示格式：`Combined`（总备弹如 `150`）或 `Split`（散弹+弹匣如 `60+90`） |

> **如果按 `Alt + R` 无反应（常见显卡热键冲突）**：部分开启了游戏内覆盖层的 N 卡（NVIDIA App / GeForce Experience）或 A 卡（AMD Software）用户，显卡驱动默认会将 `Alt + R` 占为全局性能监控热键并拦截按键，任选其一即可解决：
> 1. **关闭显卡 `Alt + R` 热键**（推荐继续用 `Alt + R`）：N 卡按 `Alt + Z → 设置 → 键盘快捷键 → 性能`，关闭或修改 `Alt + R`（A 卡在 AMD 覆盖层快捷键中同理关闭或改键）。
> 2. **在配置文件中自定义按键**（支持纯单键或任意组合键，推荐改成原版未占用的单键 `N` 或鼠标侧键 `Mouse3` / `Mouse4`）：打开 `BepInEx\config\zedzone.betterreload.cfg`，在 `[Hotkey]` 节修改后保存并重启游戏：
> ```ini
> TriggerKey = N        # 或 Mouse3 / Mouse4（对应鼠标两个侧键）
> ModifierKey = None    # 设为 None 即纯单键触发；也可填入其它修饰键组合
> ```
> 此外，本模组使用标准 BepInEx `Config.Bind` 注册配置，若已安装 BepInEx 配置界面类模组（如 ConfigurationManager），也可直接在游戏内可视化修改全部选项。

---

## 安装、联机与创意工坊 / Install, Multiplayer & Workshop

- **本地安装**：将本模组文件夹放入 `<游戏目录>\Mods\BetterReload\`，目录内包含：
  - `BetterReload.dll`
  - `mod.json`
  - `README.md`
- **联机兼容性**：所有功能均基于玩家自身本地背包（`CharacterData`）与游戏原版动作/换弹方法实现，换弹与压弹均走原版同步链路，**房主未安装本模组时客户端也可正常加入并完整使用**。
- **卸载**：删除 `Mods\BetterReload\` 文件夹，或在 `mod.json` 中将 `"enabled"` 改为 `false`。

---

## 源码编译 / Build from Source

需安装 `.NET 6+ SDK`，且游戏目录已安装 `ZedModsLoader`（BepInEx 6 IL2CPP）并运行过一次以生成 `BepInEx\interop\`：

```powershell
dotnet build src\BetterReload.csproj -c Release -p:ZedZoneDir="<你的 ZED ZONE 安装路径>"
# 或使用打包脚本：
powershell -ExecutionPolicy Bypass -File build.ps1          # 仅构建并输出到 content\BetterReload
powershell -ExecutionPolicy Bypass -File build.ps1 -Install # 构建并安装到游戏 Mods\BetterReload
```
