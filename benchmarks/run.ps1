<#
.SYNOPSIS
    Runs the benchmarks, saves a dated entry to benchmarks/history and compares it with the previous entry
    (plan PERF-007). Anything more than 10% slower is flagged.

.PARAMETER Exe
    Optional path to the built atc-mdv.exe. When given, startup (readme-5k.md) and the edit round trip are
    measured too, with benchmarks/measure.ps1.

.PARAMETER Filter
    BenchmarkDotNet filter, for example '*Render*'. Default: everything.

.PARAMETER FailOnRegression
    Exit with code 1 when anything is more than 10% slower than the previous entry.

.EXAMPLE
    pwsh -NoProfile -File benchmarks/run.ps1
    pwsh -NoProfile -File benchmarks/run.ps1 -Exe artifacts/publish/win-x64/atc-mdv.exe
#>
[CmdletBinding()]
param(
    [string] $Exe,
    [string] $Filter = '*',
    [switch] $FailOnRegression
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$historyDir = Join-Path $PSScriptRoot 'history'
$resultsDir = Join-Path $PSScriptRoot 'results'
$bdnDir = Join-Path $resultsDir 'bdn'
New-Item -ItemType Directory -Force $historyDir, $resultsDir | Out-Null

# 1. Markdown core (BenchmarkDotNet).
if (Test-Path $bdnDir) { Remove-Item $bdnDir -Recurse -Force }
dotnet run -c Release --project (Join-Path $PSScriptRoot 'Atc.MarkdownViewer.Benchmarks') -- --filter $Filter --exporters json --artifacts $bdnDir
if ($LASTEXITCODE -ne 0) { throw "BenchmarkDotNet failed (exit code $LASTEXITCODE)" }

$metrics = [ordered]@{}
foreach ($file in Get-ChildItem (Join-Path $bdnDir 'results') -Filter '*.json') {
    $report = Get-Content $file.FullName -Raw | ConvertFrom-Json
    foreach ($b in $report.Benchmarks) {
        $fixture = ($b.Parameters -replace '^Fixture=', '')
        $metrics["$($b.Method) $fixture (ms)"] = [math]::Round($b.Statistics.Mean / 1e6, 3)
        if ($b.Memory) {
            $metrics["$($b.Method) $fixture (KB alloc)"] = [math]::Round($b.Memory.BytesAllocatedPerOperation / 1KB, 0)
        }
    }
}

# 2. App startup and edit round trip, when an executable is given.
if ($Exe) {
    $fixture = Join-Path $PSScriptRoot 'fixtures/readme-5k.md'
    $measure = Join-Path $PSScriptRoot 'measure.ps1'
    pwsh -NoProfile -File $measure -Exe $Exe -Fixture $fixture -Label 'run-open' -OutDir $resultsDir | Out-Host
    pwsh -NoProfile -File $measure -Exe $Exe -Fixture $fixture -Label 'run-edit' -Edit -Runs 3 -OutDir $resultsDir | Out-Host
    $open = Get-Content (Join-Path $resultsDir 'run-open.json') -Raw | ConvertFrom-Json
    $edit = Get-Content (Join-Path $resultsDir 'run-edit.json') -Raw | ConvertFrom-Json
    $metrics['Startup readme-5k.md median (ms)'] = $open.medianMs
    $metrics['Startup readme-5k.md p90 (ms)'] = $open.p90Ms
    $metrics['Memory total readme-5k.md (MB)'] = $open.medians.PRIVATE_MB
    $metrics['Edit readme-5k.md median (ms)'] = $edit.medianMs
}

# 3. Save the entry and compare with the previous one.
$commit = (git -C $root rev-parse --short HEAD 2>$null)
$dirty = if (git -C $root status --porcelain 2>$null) { '+dirty' } else { '' }
$entry = [ordered]@{
    date    = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
    commit  = "$commit$dirty"
    machine = "$((Get-CimInstance Win32_Processor).Name.Trim()), $([System.Environment]::OSVersion.VersionString)"
    metrics = $metrics
}

$previousFile = Get-ChildItem $historyDir -Filter '*.json' | Sort-Object Name | Select-Object -Last 1
$entryFile = Join-Path $historyDir ("{0}-{1}.json" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $commit)
$entry | ConvertTo-Json -Depth 4 | Set-Content $entryFile -Encoding utf8

$regressions = 0
Write-Host ""
if ($previousFile) {
    $previous = (Get-Content $previousFile.FullName -Raw | ConvertFrom-Json).metrics
    Write-Host ("Compared with {0}" -f $previousFile.Name)
    Write-Host ("{0,-45} {1,12} {2,12} {3,9}" -f 'Metric', 'Previous', 'Now', 'Change')
    foreach ($key in $metrics.Keys) {
        $now = [double]$metrics[$key]
        $before = $previous.$key
        if ($null -eq $before -or [double]$before -eq 0) {
            Write-Host ("{0,-45} {1,12} {2,12} {3,9}" -f $key, '-', $now, 'new')
            continue
        }

        $change = ($now - [double]$before) / [double]$before
        $line = "{0,-45} {1,12} {2,12} {3,8:P0}" -f $key, $before, $now, $change
        if ($change -gt 0.10) { Write-Host "$line  SLOWER" -ForegroundColor Red; $regressions++ }
        elseif ($change -lt -0.10) { Write-Host "$line  faster" -ForegroundColor Green }
        else { Write-Host $line }
    }
}
else {
    Write-Host "First entry; nothing to compare with yet."
    $metrics.GetEnumerator() | ForEach-Object { Write-Host ("{0,-45} {1,12}" -f $_.Key, $_.Value) }
}

Write-Host ""
Write-Host "Saved $entryFile" -ForegroundColor Cyan
if ($FailOnRegression -and $regressions -gt 0) { exit 1 }
