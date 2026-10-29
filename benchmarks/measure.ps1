<#
.SYNOPSIS
    Measures launch-to-first-content-paint (or the edit round trip with -Edit) of an app that supports
    --bench/--bench-out (plan TASK-015). One warmup launch is recorded separately as firstLaunchMs.

.EXAMPLE
    pwsh benchmarks/measure.ps1 -Exe spikes/SpikeWinUI/bin/publish/w1a/spike-winui.exe -Fixture benchmarks/fixtures/readme-5k.md -Label w1a
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Exe,
    [Parameter(Mandatory)] [string] $Fixture,
    [Parameter(Mandatory)] [string] $Label,
    [int] $Runs = 15,
    [switch] $Edit,
    [string[]] $ExtraArgs = @(),
    [string] $OutDir = (Join-Path $PSScriptRoot 'results')
)

$ErrorActionPreference = 'Stop'
# pwsh -File passes "-ExtraArgs a,b" as one string; split it into separate arguments.
$ExtraArgs = @($ExtraArgs | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$Exe = (Resolve-Path $Exe).Path
$Fixture = (Resolve-Path $Fixture).Path
New-Item -ItemType Directory -Force $OutDir | Out-Null
$runFile = Join-Path $OutDir "$Label.run.txt"
$metric = if ($Edit) { 'EDIT_MEDIAN_MS' } else { 'MDV_STARTUP_MS' }

function Invoke-BenchRun {
    if (Test-Path $runFile) { Remove-Item $runFile }
    $argList = @("`"$Fixture`"", '--bench', '--bench-out', "`"$runFile`"")
    if ($Edit) { $argList += '--edit' }
    $argList += $ExtraArgs

    $process = Start-Process -FilePath $Exe -ArgumentList $argList -PassThru
    if (-not $process.WaitForExit(60000)) {
        $process.Kill($true)
        throw "Run timed out after 60 s"
    }

    # Give the WebView2 processes time to release the user data folder before the next launch.
    Start-Sleep -Milliseconds 700
    if (-not (Test-Path $runFile)) { throw "No bench output (exit code $($process.ExitCode))" }

    $values = [ordered]@{}
    foreach ($line in Get-Content $runFile) {
        $key, $value = $line -split '=', 2
        $values[$key] = $value
    }

    return $values
}

function Get-Percentile([double[]] $values, [double] $q) {
    $sorted = $values | Sort-Object
    $index = [math]::Max(0, [math]::Ceiling($q * $sorted.Count) - 1)
    return [math]::Round($sorted[$index], 1)
}

Write-Host "Warmup ($Label)..."
$first = Invoke-BenchRun
$results = @()
for ($i = 1; $i -le $Runs; $i++) {
    $results += , (Invoke-BenchRun)
    Write-Host ("  run {0,2}/{1}: {2} = {3}" -f $i, $Runs, $metric, $results[-1][$metric])
}

$numericKeys = $results[0].Keys | Where-Object { $results[0][$_] -as [double] -ne $null }
$medians = [ordered]@{}
foreach ($key in $numericKeys) {
    $medians[$key] = Get-Percentile ($results | ForEach-Object { [double]$_[$key] }) 0.5
}

$main = $results | ForEach-Object { [double]$_[$metric] }
$exeDir = Split-Path $Exe
$summary = [ordered]@{
    label           = $Label
    mode            = if ($Edit) { 'edit' } else { 'open' }
    extraArgs       = $ExtraArgs -join ' '
    fixture         = Split-Path $Fixture -Leaf
    runs            = $Runs
    metric          = $metric
    firstLaunchMs   = [math]::Round([double]$first[$metric], 1)
    medianMs        = Get-Percentile $main 0.5
    p90Ms           = Get-Percentile $main 0.9
    minMs           = [math]::Round(($main | Measure-Object -Minimum).Minimum, 1)
    maxMs           = [math]::Round(($main | Measure-Object -Maximum).Maximum, 1)
    medians         = $medians
    nonClientRegion = $results[0]['NONCLIENT_REGION']
    installSizeMB   = [math]::Round(((Get-ChildItem $exeDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
    machine         = "$((Get-CimInstance Win32_Processor).Name.Trim()), $([System.Environment]::OSVersion.VersionString)"
    date            = (Get-Date -Format 'yyyy-MM-dd')
}

Remove-Item $runFile -ErrorAction SilentlyContinue
$outFile = Join-Path $OutDir "$Label.json"
$summary | ConvertTo-Json -Depth 4 | Set-Content $outFile -Encoding utf8
Write-Host ""
Write-Host ("{0}: median {1} ms, p90 {2} ms, first launch {3} ms, install {4} MB -> {5}" -f $Label, $summary.medianMs, $summary.p90Ms, $summary.firstLaunchMs, $summary.installSizeMB, $outFile) -ForegroundColor Green
