param(
    [string]$OutName = "说唱主播助手_下载引导"
)
$ErrorActionPreference = "Stop"
$root = "C:\Users\wly\Doubao\chats\2026-11-14\new-chat\RapStreamerTool"
$build = Join-Path $root "build"
$gac = "$env:WINDIR\Microsoft.NET\assembly"
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = Join-Path $root "launcher\Launcher.cs"
$ico = Join-Path $root "assets\app.ico"
$outExe = Join-Path $build ($OutName + ".exe")

if (-not (Test-Path $build)) { New-Item -ItemType Directory -Path $build | Out-Null }

function FindGac([string]$name) {
  $paths = @()
  foreach ($sub in @("GAC_MSIL", "GAC_32", "GAC_64")) {
    $d = Join-Path $gac $sub
    if (Test-Path $d) {
      $hit = Get-ChildItem $d -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
      if ($hit) { $paths += $hit.FullName }
    }
  }
  return $paths | Select-Object -First 1
}

$refs = @()
foreach ($n in @("PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml", "System.IO.Compression", "System.IO.Compression.FileSystem")) {
  $p = FindGac $n
  if ($p) { $refs += "/r:$p" } else { Write-Host "缺少引用：$n" }
}

$args = @("/nologo", "/target:winexe", "/platform:anycpu", "/optimize+", "/warn:1", "/out:$outExe")
$args += $refs
if (Test-Path $ico) { $args += "/win32icon:$ico" }
$args += $src

Write-Host ("编译 " + $OutName + " ...")
& $csc @args
if ($LASTEXITCODE -ne 0) { Write-Host "编译失败"; exit 1 }
Write-Host ("生成：" + $outExe + "  " + [math]::Round((Get-Item $outExe).Length / 1KB) + " KB")
