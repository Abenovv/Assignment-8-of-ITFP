param([string]$Compiler = 'gcc')
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    throw 'Run this saved file with powershell -File native/build.ps1; do not paste its contents.'
}
$project = Split-Path -Parent $PSScriptRoot
$target = Join-Path $project 'runtimes\win-x64\native'
$source = Join-Path $PSScriptRoot 'planner_native.c'
$library = Join-Path $target 'planner_native.dll'
$compilerCommand = Get-Command $Compiler -ErrorAction Stop
$machine = & $compilerCommand.Source -dumpmachine
if ($LASTEXITCODE -ne 0 -or $machine -notmatch '^x86_64-.*(mingw|windows)') {
    throw 'This script needs a Windows x64 GCC compiler, such as MSYS2 UCRT64 GCC. Use build.sh on macOS or Linux.'
}
New-Item -ItemType Directory -Force -Path $target | Out-Null
& $compilerCommand.Source -std=c11 -O2 -Wall -Wextra -Werror -shared -static-libgcc '-Wl,--no-insert-timestamp' $source -o $library
if ($LASTEXITCODE -ne 0) { throw 'Native C compilation failed; read the compiler diagnostic above.' }
Write-Host "Built Windows x64 library: $library"
