#Requires -Version 7.0
<#
.SYNOPSIS
  実行系以外の Service API を 1 本ずつ、指定同時数で叩き request/sec を測る。

.DESCRIPTION
  混在負荷やサービス容量の根拠には使わない（ベストエフォート）。
  API を同時に混ぜず、プローブごとに Concurrency ワーカーを DurationSeconds 秒回す。
  既定は GET と login / validate のみ。create / delete / restore など競合する書き込みは除外する。

.PARAMETER BaseUrl
  Service API の基点 URL。

.PARAMETER Tenant
  X-Tenant-Id / ログインの tenantKey。

.PARAMETER Username
  ログインユーザー。Token / ApiKey 指定時は不要。

.PARAMETER Password
  ログインパスワード。未指定時は STATEVIA_API_TAT_PASSWORD、なければ STATEVIA_CAPACITY_PASSWORD。

.PARAMETER Token
  既存 Bearer。

.PARAMETER ApiKey
  X-Api-Key。Token より優先。

.PARAMETER Suite
  Read / Default / Admin / All。書き込みは安全なプローブだけ残す。-Only 指定時は無視する。

.PARAMETER Only
  測るプローブ ID（カンマ区切りまたは配列）。RPS 非対象は警告して除く。

.PARAMETER ListProbes
  プローブ一覧（RPS 可否付き）を出して終了する。

.PARAMETER Concurrency
  同時リクエスト数。

.PARAMETER DurationSeconds
  計測秒（ウォームアップ除外後）。

.PARAMETER WarmupSeconds
  同じ同時数で回すが記録しない秒。

.PARAMETER TimeoutSeconds
  1 リクエストの上限秒。

.PARAMETER DefinitionListLimit
  GET /v1/definitions の limit。

.PARAMETER ActionId
  GET /v1/actions/schema/{actionId} に使う actionId。

.PARAMETER Output
  JSON の出力パス。同名の CSV も書く。トークン・パスワードは書かない。

.PARAMETER HwLabel
  記録用ラベル。

.PARAMETER HwNotes
  記録用メモ。

.PARAMETER SkipCleanup
  計測用定義を消さない。

.EXAMPLE
  $env:STATEVIA_API_TAT_PASSWORD = 'admin123'
  .\scripts\measure-api-rps.ps1 -Suite Read -Output tools/api-tat/results/rps.json

.EXAMPLE
  .\scripts\measure-api-rps.ps1 -Only health.get,auth.me.get -Concurrency 16 -DurationSeconds 20
#>
[CmdletBinding()]
param(
    [string] $BaseUrl = 'http://localhost:8080',
    [string] $Tenant = 'default',
    [string] $Username = 'admin',
    [string] $Password = '',
    [string] $Token = '',
    [string] $ApiKey = '',
    [ValidateSet('Read', 'Default', 'Admin', 'All')]
    [string] $Suite = 'Read',
    [string[]] $Only = @(),
    [switch] $ListProbes,
    [ValidateRange(1, 256)]
    [int] $Concurrency = 8,
    [ValidateRange(1, 600)]
    [int] $DurationSeconds = 15,
    [ValidateRange(0, 120)]
    [int] $WarmupSeconds = 3,
    [ValidateRange(1, 600)]
    [int] $TimeoutSeconds = 30,
    [ValidateRange(1, 500)]
    [int] $DefinitionListLimit = 20,
    [string] $ActionId = 'statevia.action.builtin.execution.noop',
    [string] $Output = '',
    [string] $HwLabel = '',
    [string] $HwNotes = '',
    [switch] $SkipCleanup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'api-measure-common.ps1')

$catalog = Get-ProbeCatalog

if ($ListProbes) {
    $catalog |
        Select-Object Id, Method, PathTemplate,
        @{ Name = 'RpsSafe'; Expression = { Test-ProbeRpsSafe $_ } },
        @{ Name = 'Suites'; Expression = { $_.Suites -join ',' } } |
        Format-Table -AutoSize
    return
}

$selected = @(Resolve-SelectedProbes -Catalog $catalog)
if ($selected.Count -eq 0) {
    throw 'No probes selected.'
}

$skipped = @($selected | Where-Object { -not (Test-ProbeRpsSafe $_) })
if ($skipped.Count -gt 0) {
    Write-Warning ("RPS 対象外（競合・副作用）を除外: {0}" -f (($skipped | ForEach-Object { $_.Id }) -join ', '))
    $selected = @($selected | Where-Object { Test-ProbeRpsSafe $_ })
}

$needs = @($selected | ForEach-Object { $_.Needs } | Select-Object -Unique)
$resolvedPassword = Get-ResolvedPassword
if (($needs -contains 'Password') -and [string]::IsNullOrWhiteSpace($resolvedPassword)) {
    $selected = @($selected | Where-Object { $_.Needs -notcontains 'Password' })
    Write-Warning 'Password が無いため auth.login はスキップします。'
}

if ($selected.Count -eq 0) {
    throw 'No RPS-safe probes selected after filtering.'
}

$client = New-HttpClient -ResolvedBaseUrl $BaseUrl -RequestTimeoutSeconds $TimeoutSeconds -MaxConnectionsPerServer $Concurrency
$measuredAtUtc = [DateTime]::UtcNow.ToString('o')
$results = @()

try {
    Assert-Health -Client $client

    $needsPrincipal = $selected | Where-Object { $_.Needs -contains 'Principal' -or $_.Needs -contains 'Definition' }
    if ($needsPrincipal) {
        Connect-Principal -Client $client -ResolvedPassword $resolvedPassword
    }

    Initialize-SeedResources -Client $client -Selected $selected

    foreach ($probe in $selected) {
        Write-Host ("Measuring {0} ({1} conc, {2}s) ..." -f $probe.Id, $Concurrency, $DurationSeconds)
        $results += Measure-ProbeRps -Client $client -Probe $probe -Concurrency $Concurrency `
            -DurationSeconds $DurationSeconds -WarmupSeconds $WarmupSeconds
    }
}
finally {
    try {
        Clear-SeedResources -Client $client
    }
    catch {
        Write-Warning ("Cleanup failed: {0}" -f $_.Exception.Message)
    }

    $client.Dispose()
}

$report = [ordered]@{
    purpose            = 'best-effort-single-api-rps'
    notServiceCapacity = $true
    measuredAtUtc      = $measuredAtUtc
    gitSha             = Get-GitSha
    baseUrl            = $BaseUrl
    tenant             = $Tenant
    suite              = $(if ($Only) { 'Only' } else { $Suite })
    concurrency        = $Concurrency
    durationSeconds    = $DurationSeconds
    warmupSeconds      = $WarmupSeconds
    hwLabel            = $HwLabel
    hwNotes            = $HwNotes
    excluded           = @(
        '/v1/executions*',
        'POST /v1/events',
        'mutating probes (create/update/delete/restore/reload)',
        'PUT /v1/auth/me/password',
        'PUT /v1/admin/users/{userId}/password'
    )
    probes             = $results
}

$results |
    Select-Object @{ n = 'id'; e = { $_.id } },
    @{ n = 'method'; e = { $_.method } },
    @{ n = 'path'; e = { $_.path } },
    @{ n = 'conc'; e = { $_.concurrency } },
    @{ n = 'ok'; e = { $_.ok } },
    @{ n = 'error'; e = { $_.error } },
    @{ n = 'successRps'; e = { $_.successRps } },
    @{ n = 'attemptRps'; e = { $_.attemptRps } },
    @{ n = 'p50Ms'; e = { $_.p50Ms } },
    @{ n = 'p95Ms'; e = { $_.p95Ms } } |
    Format-Table -AutoSize

if (-not [string]::IsNullOrWhiteSpace($Output)) {
    Write-ResultFiles -Report ([pscustomobject]$report) -Path $Output -CsvProperties @(
        'id', 'method', 'path', 'concurrency', 'durationSec', 'ok', 'error', 'successRps', 'attemptRps',
        'minMs', 'p50Ms', 'p95Ms', 'maxMs', 'meanMs', 'lastError'
    )
}

$failed = @($results | Where-Object { $_.error -gt 0 })
if ($failed.Count -gt 0) {
    Write-Warning ("{0} probe(s) had HTTP errors. See lastError." -f $failed.Count)
    exit 1
}
