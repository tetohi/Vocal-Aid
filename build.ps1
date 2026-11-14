param(
    [string]$Edition = "pro",       # pro | open
    [string]$OutName = ""
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$src = Join-Path $root "src"
$build = Join-Path $root "build"
$gac = "$env:WINDIR\Microsoft.NET\assembly"
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $build)) { New-Item -ItemType Directory -Path $build | Out-Null }

$files = @(
  "AppInfo.cs", "Theme.cs", "Config.cs", "Log.cs", "UiKit.cs", "Widgets.cs", "Program.cs",
  "MainWindow.cs", "IntroWindow.cs", "PageHome.cs", "PageRecord.cs", "PageLyric.cs",
  "PageSettings.cs", "PageAbout.cs", "IconDraw.cs"
) | ForEach-Object { Join-Path $src $_ }

$files += Get-ChildItem (Join-Path $src "Engine") -Filter *.cs | Select-Object -ExpandProperty FullName

function FindGac([string]$name, [string]$arch) {
  $dir = Join-Path $gac "$arch\$name"
  if (-not (Test-Path $dir)) { return $null }
  $dll = Get-ChildItem $dir -Filter "$name.dll" -Recurse | Where-Object { $_.FullName -like "*v4.0_4.0.0.0*" } | Select-Object -First 1
  if ($dll -eq $null) { return $null }
  return $dll.FullName
}

$refs = @()
$refs += FindGac "PresentationFramework" "GAC_MSIL"
$refs += FindGac "PresentationCore" "GAC_32"
if ($refs[1] -eq $null) { $refs[1] = FindGac "PresentationCore" "GAC_64" }
$refs += FindGac "WindowsBase" "GAC_MSIL"
$refs += FindGac "System.Xaml" "GAC_MSIL"
foreach ($r in $refs) {
  if ($r -eq $null) { Write-Host "缺少 WPF 程序集引用，请确认系统已安装 .NET Framework 4.8"; exit 1 }
}

if ($OutName -eq "") {
  if ($Edition -eq "pro") { $OutName = "说唱主播助手_自用版" } else { $OutName = "说唱主播助手_开源版" }
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
