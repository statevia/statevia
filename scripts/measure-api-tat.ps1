#Requires -Version 7.0
<#
.SYNOPSIS
  実行系以外の Service API を 1 本ずつ叩き、クライアント観測 TAT を測る。

.DESCRIPTION
  同時呼び出しの混在負荷やサービス容量の根拠には使わない（ベストエフォート）。
  実行系（/v1/executions / /v1/events）は tools/capacity の対象であり、本スクリプトでは測らない。
  request/sec は scripts/measure-api-rps.ps1。

  既定スイートは読み取りと、後始末できる定義の書き込み。
  パスワード変更は常に除外する。Module reload と、削除 API の無い管理者作成は opt-in。

.PARAMETER BaseUrl
  Service API の基点 URL。

.PARAMETER Tenant
  X-Tenant-Id / ログインの tenantKey。

.PARAMETER Username
  ログインユーザー。Token / ApiKey 指定時は不要。

.PARAMETER Password
  ログインパスワード。未指定時は STATEVIA_API_TAT_PASSWORD、なければ STATEVIA_CAPACITY_PASSWORD。

.PARAMETER Token
  既存 Bearer。指定時はログインを省略する（login プローブは Password があるときだけ測る）。

.PARAMETER ApiKey
  X-Api-Key。Token より優先。

.PARAMETER Suite
  Read / Default / Admin / All。-Only 指定時は無視する。

.PARAMETER Only
  測るプローブ ID（カンマ区切りまたは配列）。

.PARAMETER ListProbes
  プローブ一覧を出して終了する。

.PARAMETER Iterations
  ウォームアップ除外後の計測回数。

.PARAMETER Warmup
  捨てる先行回数。

.PARAMETER TimeoutSeconds
  1 リクエストの上限秒。

.PARAMETER DefinitionListLimit
  GET /v1/definitions の limit（必須クエリ。1〜500）。

.PARAMETER ActionId
  GET /v1/actions/schema/{actionId} に使う actionId。

.PARAMETER Output
  JSON の出力パス。同名の CSV も書く。トークン・パスワードは書かない。

.PARAMETER HwLabel
  記録用ラベル。

.PARAMETER HwNotes
  記録用メモ（実効 vCPU / メモリなど）。

.PARAMETER SkipCleanup
  計測用定義・発行した API キーを消さない。

.EXAMPLE
  $env:STATEVIA_API_TAT_PASSWORD = 'admin123'
  .\scripts\measure-api-tat.ps1 -Output tools/api-tat/results/tat.json

.EXAMPLE
  .\scripts\measure-api-tat.ps1 -Suite Read -Iterations 50

.EXAMPLE
  .\scripts\measure-api-tat.ps1 -Only health.get,auth.me.get,definitions.list
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
    [string] $Suite = 'Default',
    [string[]] $Only = @(),
    [switch] $ListProbes,
    [ValidateRange(1, 10000)]
    [int] $Iterations = 20,
    [ValidateRange(0, 1000)]
    [int] $Warmup = 3,
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
        Select-Object Id, Method, PathTemplate, @{ Name = 'Suites'; Expression = { $_.Suites -join ',' } } |
        Format-Table -AutoSize
    return
}

$selected = @(Resolve-SelectedProbes -Catalog $catalog)
if ($selected.Count -eq 0) {
    throw 'No probes selected.'
}

$needs = @($selected | ForEach-Object { $_.Needs } | Select-Object -Unique)
$resolvedPassword = Get-ResolvedPassword
if (($needs -contains 'Password') -and [string]::IsNullOrWhiteSpace($resolvedPassword)) {
    $selected = @($selected | Where-Object { $_.Needs -notcontains 'Password' })
    Write-Warning 'Password が無いため auth.login はスキップします。'
}

if ($selected.Count -eq 0) {
    throw 'No probes selected after filtering.'
}

$client = New-HttpClient -ResolvedBaseUrl $BaseUrl -RequestTimeoutSeconds $TimeoutSeconds
$measuredAtUtc = [DateTime]::UtcNow.ToString('o')
$results = @()

try {
    Assert-Health -Client $client

    $needsPrincipal = $selected | Where-Object { $_.Needs -contains 'Principal' -or $_.Needs -contains 'Definition' }
    if ($needsPrincipal) {
        Connect-Principal -Client $client -ResolvedPassword $resolvedPassword
    }

    Initialize-SeedResources -Client $client -Selected $selected

    $groups = $selected | Group-Object Group
    foreach ($group in $groups) {
        Write-Host ("Measuring {0} ..." -f $group.Name)
        $results += Measure-ProbeGroup -Client $client -GroupProbes @($group.Group)
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
    purpose            = 'best-effort-single-api-tat'
    notServiceCapacity = $true
    measuredAtUtc      = $measuredAtUtc
    gitSha             = Get-GitSha
    baseUrl            = $BaseUrl
    tenant             = $Tenant
    suite              = $(if ($Only) { 'Only' } else { $Suite })
    iterations         = $Iterations
    warmup             = $Warmup
    hwLabel            = $HwLabel
    hwNotes            = $HwNotes
    excluded           = @(
        '/v1/executions*',
        'POST /v1/events',
        'PUT /v1/auth/me/password',
        'PUT /v1/admin/users/{userId}/password'
    )
    probes             = $results
}

$results |
    Select-Object @{ n = 'id'; e = { $_.id } },
    @{ n = 'method'; e = { $_.method } },
    @{ n = 'path'; e = { $_.path } },
    @{ n = 'ok'; e = { $_.ok } },
    @{ n = 'error'; e = { $_.error } },
    @{ n = 'minMs'; e = { $_.minMs } },
    @{ n = 'p50Ms'; e = { $_.p50Ms } },
    @{ n = 'p95Ms'; e = { $_.p95Ms } },
    @{ n = 'maxMs'; e = { $_.maxMs } },
    @{ n = 'meanMs'; e = { $_.meanMs } } |
    Format-Table -AutoSize

if (-not [string]::IsNullOrWhiteSpace($Output)) {
    Write-ResultFiles -Report ([pscustomobject]$report) -Path $Output
}

$failed = @($results | Where-Object { $_.error -gt 0 })
if ($failed.Count -gt 0) {
    Write-Warning ("{0} probe(s) had HTTP errors. See lastError." -f $failed.Count)
    exit 1
}
