# Codex OLED Monitor

<p align="center">
  <img src="assets/logo.svg" width="112" alt="Codex OLED Monitor logo">
</p>

一个原生 Windows 小工具，把 Codex 的 **5 小时**与**7 天**剩余额度持续显示在 SteelSeries Apex Pro 的 OLED 小屏幕上。

## 功能

- 通过本机 `codex app-server` 读取当前账户额度，无需 OpenAI API Key
- 通过 SteelSeries GameSense 向 128×40 OLED 推送高对比度界面
- 两条进度条分别显示 5 小时和 7 天剩余额度
- 关闭或最小化设置窗口后，仅保留系统托盘小图标，不占任务栏
- 双击托盘图标重新打开设置；右键可以立即刷新或彻底退出
- 可调额度刷新和 OLED 保活间隔
- 可选随 Windows 登录自动启动
- 单实例运行，避免多个后台进程争抢 OLED

## 运行要求

- Windows 10/11 x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- SteelSeries GG 正在运行
- Codex Desktop 或 Codex CLI 已安装并登录
- 带 OLED 屏幕的 SteelSeries Apex Pro 键盘

## 从源码运行

```powershell
dotnet run --project .\CodexOledMonitor.csproj
```

启动后关闭设置窗口，程序会隐藏到系统托盘。要彻底结束程序，请右键托盘图标并选择“退出”。

## 构建独立版

```powershell
dotnet publish .\CodexOledMonitor.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o .\publish
```

生成的 `publish\CodexOledMonitor.exe` 可在没有预装 .NET Runtime 的 Windows x64 电脑上直接运行。GitHub Actions 也会在每次提交后构建同样的可下载 artifact。

## 数据与隐私

- 不读取或保存 OpenAI API Key、浏览器 Cookie 或账户密码。
- 额度来自本机 Codex 进程的 `app-server` 协议。
- 设置保存在当前用户的本地应用数据目录。
- SteelSeries GG 的本地 GameSense 地址来自其公开的 `coreProps.json` 配置。

## 已知限制

- Codex `app-server` 接口可能随 Codex 更新发生变化。
- OLED 同一时间只能显示一个 GameSense 应用；游戏或 SteelSeries GG 设置页可能暂时抢占屏幕。
- 当前界面针对 128×40 单色 OLED 设计。

## 项目结构

- `CodexClient.cs`：读取 Codex 额度
- `GameSenseClient.cs`：注册 GameSense 游戏并发送 OLED 帧
- `OledRenderer.cs`：绘制 128×40 单色界面
- `MainForm.cs`：设置窗口、托盘行为和后台刷新循环
- `AppIcon.cs`：程序与托盘图标

本项目与 OpenAI、SteelSeries 均无官方隶属关系。
