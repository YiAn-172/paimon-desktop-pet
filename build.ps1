param(
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compilerCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw '未找到 .NET Framework C# 编译器（csc.exe）。'
}

$binDir = Join-Path $projectRoot 'bin'
$frameworkDir = Split-Path -Parent $compiler
$wpfDir = Join-Path $frameworkDir 'WPF'
New-Item -ItemType Directory -Force -Path $binDir | Out-Null
Remove-Item -LiteralPath (Join-Path $binDir 'input-smoke-result.txt') -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $binDir 'smoke-error.txt') -ErrorAction SilentlyContinue
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$output = Join-Path $binDir 'GenshinDesktopPet.exe'
$manifest = Join-Path $projectRoot 'app.manifest'
$icon = Join-Path $projectRoot 'assets\icon.ico'

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    "/out:$output",
    "/win32manifest:$manifest",
    "/win32icon:$icon",
    "/reference:$(Join-Path $frameworkDir 'System.dll')",
    "/reference:$(Join-Path $frameworkDir 'System.Core.dll')",
    "/reference:$(Join-Path $frameworkDir 'System.Drawing.dll')",
    "/reference:$(Join-Path $frameworkDir 'System.Windows.Forms.dll')",
    "/reference:$(Join-Path $frameworkDir 'System.Web.Extensions.dll')",
    "/reference:$(Join-Path $frameworkDir 'System.Security.dll')",
    "/reference:$(Join-Path $wpfDir 'WindowsBase.dll')",
    "/reference:$(Join-Path $wpfDir 'PresentationCore.dll')",
    "/reference:$(Join-Path $wpfDir 'PresentationFramework.dll')",
    "/reference:$(Join-Path $frameworkDir 'System.Xaml.dll')"
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出代码：$LASTEXITCODE"
}

$runtimeAssets = Join-Path $binDir 'assets'
if (Test-Path -LiteralPath $runtimeAssets) {
    Remove-Item -Recurse -Force -LiteralPath $runtimeAssets
}
Copy-Item -Recurse -Force -LiteralPath (Join-Path $projectRoot 'assets') -Destination $runtimeAssets
Copy-Item -Force -LiteralPath (Join-Path $projectRoot 'characters.json') -Destination (Join-Path $binDir 'characters.json')
Copy-Item -Force -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $binDir '使用说明.md')

Write-Host "构建完成：$output"

if ($Publish) {
    $portableDir = Join-Path $projectRoot 'portable'
    if (Test-Path -LiteralPath $portableDir) {
        Remove-Item -Recurse -Force -LiteralPath $portableDir
    }
    New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
    Copy-Item -Recurse -Force -Path (Join-Path $binDir '*') -Destination $portableDir
    Write-Host "便携版：$portableDir"
}
