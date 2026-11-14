# 说唱主播助手

说唱主播用的桌面辅助工具（Windows）。检测浏览器里短视频的播放开始／结束，自动录屏＋麦克风＋系统原声，并把视频台词与主题色显示在软件内。

- 纯 C# 5 + WPF（代码式界面，无 XAML 文件）+ 单命令编译
- 零第三方依赖：只要系统自带 .NET Framework 4.8，`csc.exe` 直接编译出免安装单文件 exe
- 界面仅简体中文

## 功能

1. **播放状态检测**：监听系统音频电平 + 枚举浏览器窗口（Chrome / Edge / FireFox / 豆包浏览器等），声音持续 0.5s 判为开始，静音 2.2s 判为结束。
2. **画面与合成**：`PrintWindow` 抓浏览器窗口（黑屏自动回退 `CopyFromScreen`），MJPEG 编码，与 PCM 音频一起封进 AVI。
3. **录音对齐**：麦克风（waveIn）+ 系统回环（WASAPI Loopback）双轨，录制起点对齐视频起点，按 `latencyMs` 扣除延迟，支持自动校准。
4. **台词**：PowerShell 投影 WinRT `OcrEngine` 识别字幕区域文字；背景取画面主色，渐变填充。
5. 以上全部只在点击「开始检测」后启动。

## 版本

| 版本 | 定义 | 版本号 | 帧率上限 |
|---|---|---|---|
| 开源版 | 默认 | v1.0.0 | 15 |

## 编译

```powershell
# 开源版
.\build.ps1 -Edition open
```

输出到 `build\说唱主播助手_开源版.exe`。
脚本使用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，`assets\app.ico` 存在时自动嵌入图标。

## 目录结构

```
src\
  Program.cs        入口、DPI、单实例、首次引导、异常兜底、--shot/--selftest
  MainWindow.cs     自定义标题栏、左侧导航、页面切换动画
  Theme.cs  UiKit.cs  Widgets.cs   配色、控件工厂（按钮/开关/滑块/下拉自绘模板）
  AppInfo.cs  Config.cs  Log.cs    版本常量、ini 配置、日志总线
  PageHome.cs  PageRecord.cs  PageLyric.cs  PageSettings.cs  PageAbout.cs  IntroWindow.cs
  IconDraw.cs       矢量 Logo（麦克风）
  Engine\
    Native.cs         Win32 声明
    CoreAudio.cs      WASAPI：设备峰值 + Loopback 采集
    MicRecorder.cs    waveIn 麦克风采集
    ScreenGrabber.cs  窗口截图、JPEG 编码、主色提取、字幕区域裁切
    AviWriter.cs      AVI（MJPEG + PCM）写入
    OcrWorker.cs      PowerShell 常驻进程 + WinRT OCR
    BrowserWatcher.cs 浏览器窗口枚举与平台识别
    Session.cs        状态机：检测 → 录制 → 收尾
assets\app.ico      应用图标（16~256 多尺寸）
build.ps1           csc 编译脚本
```

## 已知限制

- 录制容器为 **AVI（MJPEG + PCM）**，未集成 ffmpeg，不做 MP4 转码；剪映／PR 可直接导入。
- 音频设备（waveIn / WASAPI 回环）在部分机器上首次打开较慢，已在后台线程执行并做降级处理，不影响画面录制。
- 主题色取自画面主色，滚动字幕场景下会有轻微跳变。

## 使用声明

- 本工具仅供个人学习、练唱与作品参考使用；录制内容的版权归原作者及原平台所有。
- 请勿将录制内容用于商业用途或公开传播。如需转载、二创发布，请先取得权利人授权。
- 程序仅通过系统屏幕与音频接口采集，不破解、不绕过任何加密或版权保护措施。
- 使用本工具产生的一切后果由使用者自行承担。

## 许可

MIT（可自行替换）。
