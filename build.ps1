param(
    [string]$Edition = "pro",       # pro | open
    [string]$OutName = ""
)
$ErrorActionPreference = "Stop"
$root = "C:\Users\wly\Doubao\chats\2026-11-14\new-chat\RapStreamerTool"
$src = Join-Path $root "src"
$build = Join-Path $root "build"
$gac = "$env:WINDIR\Microsoft.NET\assembly"
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $build)) { New-Item -ItemType Directory -Path $build | Out-Null }

$files = @(
  "AppInfo.cs", "Theme.cs", "Config.cs", "Log.cs", "UiKit.cs", "Widgets.cs", "Program.cs",
  "MainWindow.cs", "IntroWindow.cs", "PageHome.cs", "PageRecord.cs", "PageLyric.cs",
  "PageSettings.cs", "PageAbout.cs", "PageCrawler.cs", "IconDraw.cs"
) | ForEach-Object { Join-Path $src $_ }

$files += Get-ChildItem (Join-Path $src "Engine") -Filter *.cs | Select-Object -ExpandProperty FullName

$refs = @(
  "$gac\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll",
  "$gac\GAC_32\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll",
  "$gac\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll",
  "$gac\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"
)

if ($OutName -eq "") {
  if ($Edition -eq "pro") { $OutName = "说唱主播助手_自用版" } else { $OutName = "说唱主播助手" }
}
$outExe = Join-Path $build ($OutName + ".exe")
$ico = Join-Path $root "assets\app.ico"

$args = @("/nologo", "/target:winexe", "/platform:anycpu", "/optimize+", "/warn:1", "/out:$outExe")
if ($Edition -eq "pro") { $args += "/define:EDITION_PRO" }
foreach ($r in $refs) { $args += "/r:$r" }
if (Test-Path $ico) { $args += "/win32icon:$ico" }
$args += $files

Write-Host ("编译 " + $OutName + " ...")
& $csc @args
if ($LASTEXITCODE -ne 0) { Write-Host "编译失败"; exit 1 }
Write-Host ("生成：" + $outExe + "  " + [math]::Round((Get-Item $outExe).Length / 1KB) + " KB")
