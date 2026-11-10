# WindowScatter

A Windows utility that brings macOS Exposé-style window management to your desktop. Trigger it with a hotkey or hot corner and all your open windows fan out into a live thumbnail overview. Click one to switch to it, press Escape to dismiss.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![Framework](https://img.shields.io/badge/.NET-8.0-purple)
![WPF](https://img.shields.io/badge/UI-WPF-blueviolet)

---

## Features

- **Live DWM thumbnails**: windows are rendered in real-time using the Desktop Window Manager API, not static screenshots
- **Scatter animation**: windows animate from their original positions to the overview and back
- **稳定的空间布局**：所有预览统一缩放，保留窗口之间的大小比例和各自宽高比，优先靠近原位置；允许少量交叠，不再随机散开
- **Keyboard navigation**: arrow keys, Tab, and Enter to navigate and select without touching the mouse (still needs some work)
- **Hot corners**: move your cursor to a screen corner to trigger the overview automatically
- **Configurable hotkey**: default is `Win+W`, changeable in `settings.json`
- **Desktop capture background**: blurred live desktop snapshot as the overlay background, falls back to wallpaper if unavailable
- **Multi-monitor aware**: scatter view opens on whichever monitor your cursor is on
- **Tray icon**: the app stays recallable while hidden (double-click to scatter); menu has Run-on-startup toggle and Exit
- **Run on startup**: optional `RunOnStartup` setting (HKCU Run key), applied on launch

---

## Requirements

- Windows 10 or Windows 11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (Desktop Runtime)

---

## Getting Started

### Build from source

```bash
git clone https://github.com/miguelo96/WindowScatter.git
cd WindowScatter
dotnet build -c Release
dotnet run
```

Or open `WindowScatter.csproj` in Visual Studio 2022+ and hit Run.

Or just download the zip folder on releases and run WindowScatter.exe.

### Run

The app runs silently in the background with no visible window. Once running:

1. Press **Win+W** (default) to trigger the scatter view
2. Click a thumbnail to switch to that window
3. 按 **Escape**、点击空白处或再次按激活快捷键，退出总览而不切换窗口

### 此 fork 的布局与退出行为

- 大窗口的预览仍然比小窗口大；同一次总览内只使用一个缩放系数，不单独放大、压扁或缩小某个预览。
- 以窗口在当前显示器上的原始中心为参考，边缘或跨屏窗口会被限制在总览可见范围内。位置调整优先选择离原中心较近的候选，不承诺拥挤时所有左右、上下关系都不变。
- 任意两个预览的交叠面积最多为较小预览面积的 **5%**，阈值集中定义在 `WindowLayoutCalculator.MaxOverlapFraction`，暂不提供配置项。这个限制针对预览矩形，不包括标题和高亮装饰。
- 候选位置仍然放不下时，统一缩小所有窗口后重新尝试。极端情况下若贪心搜索仍失败，则在可容纳全部窗口的统一比例下按原始横向顺序排列作为安全兜底；不是正常情况下的固定网格模式。
- 相同的窗口句柄、原始矩形和屏幕尺寸得到相同布局，改变窗口枚举顺序不会让布局随机变化。每次打开都使用最新几何数据，不复用只按句柄判断的旧缓存。
- 快捷键、托盘菜单/双击和热角重新进入都可切换总览的开关状态。热角每次进入只触发一次，持续停留不会自动关闭；需先离开再进入，并受原有冷却时间限制。
- 打开动画期间点击空白、按 Escape 或再次激活，会在打开完成后退出；收起期间的重复请求会被忽略。长按激活键不会反复开关。
- 点击预览仍会激活对应窗口；点击空白退出不会主动激活另一个窗口。

### 验证与测试

独立测试项目使用 **.NET 10 SDK**，可在 Linux/WSL 或 Windows 上运行；主程序的目标框架仍是 .NET 8 Windows，没有升级主程序运行时要求。

```bash
dotnet test tests/WindowScatter.LayoutTests/WindowScatter.LayoutTests.csproj

# 在 Linux/WSL 中只交叉构建主程序，不运行 Windows 界面
dotnet build WindowScatter.csproj -c Release -p:EnableWindowsTargeting=true
```

测试直接链接实际布局算法、输入策略及数据模型的源码，仅为未被调用的 WPF 控件类型提供占位定义，不调用 Win32/DWM。覆盖统一比例、少量交叠、边界、枚举顺序稳定性、移动/缩放更新、20 个窗口堆叠、100 组固定种子的随机桌面，以及快捷键去重和延迟退出。它们不替代 Windows UI 集成测试。

Windows 实机验收清单（需手工运行，交叉构建无法验证）：

1. 摆放不同大小的窗口，连续呼出多次，核对大小比例、原始方位和稳定性；移动/调整窗口大小后再呼出，布局应随之更新。
2. 分别用快捷键再次按下、空白点击、Escape、托盘和热角重新进入退出；确认没有切换到未点击的窗口。
3. 长按快捷键、快速重复激活、打开动画中请求退出，确认不会反复开关或卡在总览。鼠标停留在热角超过冷却时间，不应自动关闭。
4. 点击预览、键盘选择后按 Enter，确认仍能切换；退出动画完成后确认再次呼出正常。
5. 在 Windows 10/11、多显示器（含负坐标）及 100%/150%/200% DPI 下核对预览、点击区域和空白区域是否一致。

如需从 WSL 生成 Windows x64 的独立运行目录，可执行下列命令；生成后把整个目录复制到 Windows 运行，而不是在 Linux 下运行 WPF 程序：

```bash
dotnet publish WindowScatter.csproj -c Release -r win-x64 --self-contained true \
  -p:EnableWindowsTargeting=true -o artifacts/win-x64
```

---

## Configuration

Settings are stored in `settings.json` next to the executable. The file is created automatically on first run with defaults.

```json
{
  "Hotkey": "Win+W",
  "EnableHotCorners": true,
  "HotCornerPosition": "TopLeft",
  "HotCornerDelay": 500,
  "AnimationSpeed": 0.25
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hotkey` | string | `"Win+W"` | Keyboard shortcut to trigger scatter. Supports modifiers: `Win`, `Ctrl`, `Alt`, `Shift` |
| `EnableHotCorners` | bool | `true` | Whether moving the cursor to a screen corner triggers scatter |
| `HotCornerPosition` | string | `"TopLeft"` | Which corner activates scatter. Options: `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight` |
| `HotCornerDelay` | int | `500` | Milliseconds the cursor must stay in the corner before triggering |
| `AnimationSpeed` | double | `0.25` | Duration of scatter/return animation in seconds |
| `RunOnStartup` | bool | `false` | Launch at logon via HKCU Run key (also toggleable from the tray menu) |

### Hotkey format

Modifiers and key separated by `+`. Examples:

```
Win+W
Ctrl+Alt+Tab
Win+Shift+E
```

---

## Keyboard Controls (while scatter is active)

| Key | Action |
|---|---|
| `Tab` / `Shift+Tab` | Cycle through windows forward / backward |
| `Arrow keys` | Navigate to the nearest window in that direction |
| `Enter` | Switch to the selected window |
| `Escape` | Dismiss scatter without switching |

---

## Project Structure

```
WindowScatter/
├── MainWindow.xaml / .cs          # App entry point, orchestrates all managers
├── WindowLayoutCalculator.cs      # Scatter layout algorithm (placement & sizing)
├── WindowAnimationManager.cs      # Timer-based animation loop (scatter & return)
├── ThumbnailManager.cs            # DWM thumbnail registration and canvas elements
├── WindowSwitcher.cs              # Handles restoring a window after selection
├── WindowEnumerator.cs            # Enumerates visible, scatter-eligible windows
├── DesktopCaptureManager.cs       # Live desktop background capture via DWM
├── HotCornerManager.cs            # Cursor polling for hot corner activation
├── MonitorHelper.cs               # Monitor detection and bounds from cursor position
├── WallpaperManager.cs            # Wallpaper fallback for background image
├── HotkeyParser.cs                # Parses hotkey strings into virtual key codes
├── AppSettings.cs                 # Settings model with JSON load/save
├── WindowInfo.cs                  # Data model for an enumerated window
├── Win32Interop.cs                # P/Invoke declarations (DWM, User32, etc.)
└── App.xaml / .cs                 # WPF application bootstrap
```

---

## How It Works

1. A low-level keyboard hook intercepts the configured hotkey system-wide
2. On trigger, `WindowEnumerator` lists all visible windows on the active monitor
3. `WindowLayoutCalculator` 使用最高 50% 的统一缩放比例计算位置，优先保留原始中心和相对大小，允许最多 5% 的两两交叠；拥挤时整体继续缩小，没有 15% 的硬下限
4. `DesktopCaptureManager` captures a live DWM thumbnail of the desktop as the blurred background
5. `ThumbnailManager` registers a DWM thumbnail for each window and places transparent click-capture borders over them on a WPF Canvas
6. `WindowAnimationManager` runs an animation loop interpolating each thumbnail from its real position to its scatter position
7. On click or keyboard Enter, `WindowSwitcher` runs the reverse animation and calls `SetForegroundWindow` on the chosen handle

---

## Known Limitations

- Windows only. Relies heavily on DWM and Win32 APIs
- No settings UI (yet). Configuration requires manually editing `settings.json` (or the tray menu for startup)
- No much visual

---

## License

MIT
