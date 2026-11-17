# TaskbarController · OLED 任务栏助手

在 Windows x64 上，根据笔记本内屏是否参与桌面来切换任务栏的自动隐藏设置。

从 [GitHub Releases](https://github.com/XIAYE1020/TaskbarController/releases/latest) 下载 `TaskbarController.exe`。双击运行后，程序会进入自动模式并驻留在右下角托盘，图标可能位于“^”折叠菜单中。无需安装或管理员权限。

| 实际启用的屏幕 | 自动隐藏任务栏 |
| --- | --- |
| 只有笔记本内屏 | 开启 |
| 内屏 + 外接屏（扩展或复制） | 开启 |
| 只有 HDMI / DP 外接屏 | 关闭 |
| 未能可靠识别屏幕类型 | 保留当前状态并提示 |

根据 Windows 报告的活动显示路径判断，而不是仅看是否插着线。“内屏启用”指内屏参与 Windows 桌面；把亮度降到零、屏幕显示黑色或合盖但仍被 Windows 启用，不等于停用内屏。用 Win+P → 仅第二屏幕，可以明确停用内屏。

每 2 秒检查一次，连续两次检测一致后应用，通常在切换显示器后 2–4 秒生效。系统任务栏尚未启动、休眠恢复和插拔过程中的短暂错误会重试。调用 Windows 的正式任务栏 API，无需重启资源管理器。自动隐藏是系统任务栏设置，本工具不对不同屏幕分别设置。

## 手动切换

- 双击托盘图标：切换显示 / 隐藏，同时暂停自动模式。
- 双击本地编译生成的 `Toggle Taskbar.lnk`：同样切换一次；若自动程序未运行，则仅切换一次后退出。
- 右键托盘图标 → “自动模式：内屏启用时隐藏”：恢复自动控制。
- 右键菜单提供“手动：自动隐藏”和“手动：始终显示”。

可把 `Toggle Taskbar.lnk` 复制到桌面，并在快捷方式属性里设置快捷键，例如 Ctrl+Alt+T。仅下载 EXE 时，也可以创建指向该 EXE 并带有 `--toggle` 参数的快捷方式。

## 开机启动和退出

右键托盘图标，勾选“登录 Windows 后自动启动”即可（默认为关闭）。再次点击取消。启动项仅写入当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\OledTaskbarController`。启用后请不要移动此文件夹；若需移动，先取消自启，移动后重新勾选。

“退出并恢复启动前设置”或 `Stop and Restore.lnk` 会退出程序并恢复本次启动前的任务栏设置；“退出并保留当前设置”只停止自动控制。强制结束进程不会恢复。重新启动程序会重新进入自动模式。

删除前先取消自启，再从托盘退出，然后删除文件夹即可。

## 源码、验证和命令行

在项目目录中运行以下命令。编译使用 Windows 的 .NET Framework C# 编译器，程序没有外部依赖或联网功能。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

默认生成 `TaskbarController.exe` 和三个本机快捷方式。`Start Automatic.lnk` 可直接启动自动模式。仅生成 EXE 时，在编译命令后加 `-SkipShortcuts`。需要重新生成 `TaskbarController.ico` 时，可在 Windows PowerShell 中运行 `generate-icon.ps1`。

命令支持 `--auto`、`--toggle`、`--hide`、`--show`、`--exit`。另有只读诊断 `--diagnose [输出文件路径]` 和内置测试 `--self-test [输出文件路径]`。

实现依据：[QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig)、[显示器连接类型](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ne-wingdi-displayconfig_video_output_technology)、[ABM_SETSTATE](https://learn.microsoft.com/en-us/windows/win32/shell/abm-setstate)。
