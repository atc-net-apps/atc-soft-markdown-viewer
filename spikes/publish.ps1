<#
.SYNOPSIS
    Publishes the WinUI 3 host spike variants (plan DEC-001) into spikes/SpikeWinUI/bin/publish/<variant>.

.EXAMPLE
    pwsh -NoProfile -File spikes/publish.ps1
    pwsh -NoProfile -File spikes/publish.ps1 -Variant w1a,w1b
#>
[CmdletBinding()]
param(
    # Accepts a list or one comma-separated string (pwsh -File passes "w1a,w1b" as a single argument).
    [string[]] $Variant = @('w1a', 'w1b', 'w1a-aot')
)

$ErrorActionPreference = 'Stop'
$Variant = $Variant | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$unknown = $Variant | Where-Object { $_ -notin @('w1a', 'w1b', 'w1a-aot') }
if ($unknown) { throw "Unknown variant(s): $($unknown -join ', '). Use w1a, w1b or w1a-aot." }
$projectDir = Join-Path $PSScriptRoot 'SpikeWinUI'
$project = Join-Path $projectDir 'SpikeWinUI.csproj'

$properties = @{
    'w1a'     = @('-p:WasdkSelfContained=true')
    'w1b'     = @('-p:WasdkSelfContained=false')
    'w1a-aot' = @('-p:WasdkSelfContained=true', '-p:PublishAot=true')
}

foreach ($v in $Variant) {
    # Variants change restore/build inputs, so start each one from a clean obj/bin.
    foreach ($dir in @('obj', 'bin/Release', 'bin/x64')) {
        $path = Join-Path $projectDir $dir
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }

    $out = Join-Path $projectDir "bin/publish/$v"
    Write-Host "=== $v -> $out" -ForegroundColor Cyan
    dotnet publish $project -c Release @($properties[$v]) -o $out
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "$v failed (exit code $LASTEXITCODE)"
    }
}
