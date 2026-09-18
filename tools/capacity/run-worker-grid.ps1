# Worker プロセス数 × スロット（L1）と Cancel 角（C1）の縮小格子。
# ui-studio は起動しない。パスワードは STATEVIA_CAPACITY_PASSWORD（未設定時は Development の admin123）。
[CmdletBinding()]
param(
    [ValidateSet('L1', 'C1', 'all')]
    [string] $Series = 'all',
    [string] $ResultsDir = '',
    [string] $SkipUntil = '',
    [int] $L1Count = 512,
    [int] $C1Count = 128,
    [int] $HttpConcurrency = 16,
    [int] $TimeoutSeconds = 300,
    [int] $SettleMs = 3000,
    [string] $BaseUrl = 'http://localhost:8080',
    [string] $HwLabel = 'ref-dev',
    [string] $HwNotes = 'Windows, 16 logical CPUs, ~80 GiB',
    [string] $Topology = 'split-runtime'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $ResultsDir) {
    $stamp = Get-Date -Format 'yyyyMMddHHmmss'
    $ResultsDir = Join-Path $RepoRoot "tools\capacity\results\grid-$stamp"
}

New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
$SummaryPath = Join-Path $ResultsDir 'summary.csv'
if (-not (Test-Path $SummaryPath)) {
    Set-Content -Path $SummaryPath -Encoding utf8 -Value 'cell,scenario,replicas,slots,loops,exitCode,completedRate,startAcceptRate,startP95Ms,cancelledRate,cancelAcceptP95Ms,completedCount,cancelledCount,http5xx,jsonPath'
}

if (-not $env:STATEVIA_CAPACITY_PASSWORD) {
    $env:STATEVIA_CAPACITY_PASSWORD = 'admin123'
}

function Invoke-Compose {
    param([string[]] $ComposeArgs)
    Push-Location $RepoRoot
    try {
        & docker compose -f docker-compose.yml -f docker-compose.split-runtime.yml @ComposeArgs
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose failed: $($ComposeArgs -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Wait-ApiHealthy {
    $deadline = (Get-Date).AddSeconds(90)
    do {
        try {
            $health = Invoke-RestMethod -Uri "$BaseUrl/v1/health" -TimeoutSec 5
            if ($health.status -eq 'ok') {
                return
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)
    throw "Service API was not healthy at $BaseUrl/v1/health"
}

function Wait-WorkerReplicas {
    param([int] $Expected)
    $deadline = (Get-Date).AddSeconds(90)
    do {
        Push-Location $RepoRoot
        try {
            $ids = & docker compose -f docker-compose.yml -f docker-compose.split-runtime.yml ps worker --status running -q
        }
        finally {
            Pop-Location
        }
        $count = @($ids | Where-Object { $_ }).Count
        if ($count -eq $Expected) {
            return
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    throw "Expected $Expected running worker replica(s)."
}

function Invoke-DrainRunning {
    $login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/auth/login" -ContentType 'application/json' -Body (
        @{ tenantKey = 'default'; username = 'admin'; password = $env:STATEVIA_CAPACITY_PASSWORD } | ConvertTo-Json
    )
    $headers = @{
        Authorization = "Bearer $($login.accessToken)"
        'X-Tenant-Id' = 'default'
    }
    $offset = 0
    $ids = New-Object System.Collections.Generic.List[string]
    do {
        $page = Invoke-RestMethod -Headers $headers -Uri "$BaseUrl/v1/executions?limit=200&offset=$offset&status=Running"
        foreach ($item in @($page.items)) {
            if ($item.displayId) {
                $ids.Add([string]$item.displayId)
            }
        }

        if (-not $page.hasMore) {
            break
        }

        $offset += [int]$page.limit
    } while ($true)

    foreach ($id in $ids) {
        try {
            Invoke-WebRequest -Method Post -Headers $headers -Uri "$BaseUrl/v1/executions/$id/cancel" -ContentType 'application/json' -Body '{}' -TimeoutSec 30 | Out-Null
        }
        catch {
            Write-Host "drain cancel failed: $id"
        }
    }

    if ($ids.Count -gt 0) {
        Write-Host "drained $($ids.Count) Running execution(s)"
        Start-Sleep -Seconds 5
    }
}

function Set-WorkerTopology {
    param([int] $Replicas, [int] $Slots, [int] $Loops)
    $env:STATEVIA_WORKER_MAX_CONCURRENCY = [string]$Slots
    $env:STATEVIA_WORKER_CANCEL_CONCURRENCY = [string]$Loops
    Invoke-Compose -ComposeArgs @('up', '-d', 'postgres', 'service-api', 'action-host', 'scheduler')
    Invoke-Compose -ComposeArgs @('up', '-d', '--scale', "worker=$Replicas", '--force-recreate', '--no-deps', 'worker')
    Wait-WorkerReplicas -Expected $Replicas
    Wait-ApiHealthy
}

function Get-Metric {
    param($Metrics, [string] $Name)
    if ($null -eq $Metrics) {
        return ''
    }

    $prop = $Metrics.PSObject.Properties[$Name]
    if ($null -eq $prop) {
        return ''
    }

    return $prop.Value
}

function Invoke-GridCell {
    param(
        [string] $CellId,
        [string] $Scenario,
        [int] $Replicas,
        [int] $Slots,
        [int] $Loops,
        [int] $Count
    )

    Write-Host "=== $CellId replicas=$Replicas slots=$Slots loops=$Loops count=$Count ==="
    Set-WorkerTopology -Replicas $Replicas -Slots $Slots -Loops $Loops
    Invoke-DrainRunning

    $jsonPath = Join-Path $ResultsDir "$CellId.json"
    $project = Join-Path $RepoRoot 'tools\capacity\Statevia.Tools.Capacity'
    $args = @(
        'run', '--project', $project, '--',
        '--scenario', $Scenario,
        '--base-url', $BaseUrl,
        '--tenant', 'default',
        '--username', 'admin',
        '--count', [string]$Count,
        '--concurrency', [string]$HttpConcurrency,
        '--timeout-seconds', [string]$TimeoutSeconds,
        '--settle-ms', [string]$SettleMs,
        '--hw-label', $HwLabel,
        '--hw-notes', $HwNotes,
        '--topology', $Topology,
        '--worker-replicas', [string]$Replicas,
        '--worker-max-concurrency', [string]$Slots,
        '--worker-cancel-concurrency', [string]$Loops,
        '--output', $jsonPath
    )
    Push-Location $RepoRoot
    try {
        & dotnet @args
        $exit = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $completedRate = ''
    $startAcceptRate = ''
    $startP95 = ''
    $cancelledRate = ''
    $cancelP95 = ''
    $completedCount = ''
    $cancelledCount = ''
    $http5xx = ''
    if (Test-Path $jsonPath) {
        $result = Get-Content -Raw -Path $jsonPath | ConvertFrom-Json
        $completedRate = Get-Metric $result.metrics 'completedRatePerSecond'
        $startAcceptRate = Get-Metric $result.metrics 'startAcceptRatePerSecond'
        $startP95 = Get-Metric $result.metrics 'startAcceptP95Ms'
        $cancelledRate = Get-Metric $result.metrics 'cancelledRatePerSecond'
        $cancelP95 = Get-Metric $result.metrics 'cancelAcceptP95Ms'
        $completedCount = Get-Metric $result.metrics 'completedCount'
        $cancelledCount = Get-Metric $result.metrics 'cancelledCount'
        $http5xx = Get-Metric $result.metrics 'http5xxCount'
    }

    Add-Content -Path $SummaryPath -Encoding utf8 -Value "$CellId,$Scenario,$Replicas,$Slots,$Loops,$exit,$completedRate,$startAcceptRate,$startP95,$cancelledRate,$cancelP95,$completedCount,$cancelledCount,$http5xx,$jsonPath"
    Invoke-DrainRunning
    if ($exit -ne 0) {
        Write-Host "cell $CellId exited $exit (recorded; continuing)"
    }
}

$cells = [System.Collections.Generic.List[object]]::new()
if ($Series -in @('L1', 'all')) {
    foreach ($replicas in @(1, 2, 3, 4)) {
        foreach ($slots in @(8, 16, 32, 64)) {
            $cells.Add([pscustomobject]@{
                    Id       = "L1-p$replicas-s$slots-c1"
                    Scenario = 'L1'
                    Replicas = $replicas
                    Slots    = $slots
                    Loops    = 1
                    Count    = $L1Count
                })
        }
    }
}

if ($Series -in @('C1', 'all')) {
    foreach ($pair in @(
            @{ Replicas = 1; Slots = 8 },
            @{ Replicas = 1; Slots = 64 },
            @{ Replicas = 4; Slots = 8 },
            @{ Replicas = 4; Slots = 64 }
        )) {
        foreach ($loops in @(1, 2, 4, 8)) {
            $cells.Add([pscustomobject]@{
                    Id       = "C1-p$($pair.Replicas)-s$($pair.Slots)-c$loops"
                    Scenario = 'C1'
                    Replicas = $pair.Replicas
                    Slots    = $pair.Slots
                    Loops    = $loops
                    Count    = $C1Count
                })
        }
    }
}

$started = [string]::IsNullOrWhiteSpace($SkipUntil)
Write-Host "results: $ResultsDir"
Write-Host "cells: $($cells.Count) series=$Series"
foreach ($cell in $cells) {
    if (-not $started) {
        if ($cell.Id -eq $SkipUntil) {
            $started = $true
        }
        else {
            Write-Host "skip $($cell.Id)"
            continue
        }
    }

    Invoke-GridCell -CellId $cell.Id -Scenario $cell.Scenario -Replicas $cell.Replicas -Slots $cell.Slots -Loops $cell.Loops -Count $cell.Count
}

Write-Host 'restore worker replicas=1 slots=16 loops=1'
Set-WorkerTopology -Replicas 1 -Slots 16 -Loops 1
Write-Host "done. summary: $SummaryPath"
