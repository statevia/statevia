#Requires -Version 5.1
<#
.SYNOPSIS
  runtime（Statevia.Runtime / Scheduler / Worker）向け SonarScanner を実行する。

.DESCRIPTION
  HostedService 正本と分離ホストを statevia_statevia_runtime として解析する。
  カバレッジは Api.Tests（DelayWait Scheduler 等）を dotnet/service/api から収集する。
  解析対象は dotnet/service/runtime に限定し、API / Engine 等と二重計上しない。
  sonar.projectBaseDir は dotnet/（ファイルのキーを集約前と同じに保つ）。Scanner for .NET の scanAll（既定 true）は、MSBuild のプロジェクト外のファイル（Dockerfile や設定）も解析に混ぜるため、オフにする。
  API sln をビルドするため、build / test に /p:StateviaSonarScope=runtime を渡し担当外プロジェクトを SonarQubeExclude する。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
  送信先は SonarScanner.Common.ps1（既定は SonarQube Cloud）。
  プロジェクトキー: statevia_statevia_runtime
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SonarScanner.Common.ps1')

# 解析は dotnet/service/runtime のみ。依存ソースは除外する。
$sonarAnalysisExclusions = @(
    '**/service/api/**',
    '**/service/cli/**',
    '**/service/action-host/**',
    '**/core/**',
    '**/infrastructure/**',
    '**/nodejs/studio/**',
    '**/tests/**',
    '**/Migrations/**',
    '**/*.Tests/**',
    '**/Dockerfile'
) -join ','
$sonarCoverageExclusions = $sonarAnalysisExclusions

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetRoot = Join-Path $repoRoot 'dotnet'
$runtimeDir = Join-Path $dotnetRoot 'service\runtime'
$apiDir = Join-Path $dotnetRoot 'service\api'
$coverageXml = Join-Path $PSScriptRoot 'core-runtime-coverage.xml'

if (-not (Test-Path -LiteralPath $runtimeDir -PathType Container)) {
    Write-Error "dotnet/service/runtime ディレクトリが見つかりません: $runtimeDir"
    exit 1
}

if (-not (Test-Path -LiteralPath $apiDir -PathType Container)) {
    Write-Error "dotnet/service/api ディレクトリが見つかりません: $apiDir"
    exit 1
}

Push-Location -LiteralPath $apiDir
try {
    $beginArguments = Get-StateviaSonarBeginArguments `
        -ProjectKey 'statevia_statevia_runtime' `
        -ProjectBaseDir $dotnetRoot `
        -CoverageReportPath $coverageXml `
        -Inclusions '**/service/runtime/**' `
        -AnalysisExclusions $sonarAnalysisExclusions `
        -CoverageExclusions $sonarCoverageExclusions `
        -ExcludeTestProjects
    dotnet sonarscanner @beginArguments
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonarscanner begin failed'
        exit 1
    }

    # Runtime / Scheduler / Worker は statevia-api.sln に含まれる。
    dotnet build 'statevia-api.sln' '/p:StateviaSonarScope=runtime'
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] build failed'
        exit 1
    }

    # HostedService の単体テストは Api.Tests 側にある。
    dotnet-coverage collect 'dotnet test Statevia.Service.Api.Tests/Statevia.Service.Api.Tests.csproj /p:StateviaSonarScope=runtime --filter FullyQualifiedName~DelayWaitSchedulerHostedServiceTests|FullyQualifiedName~OptionsValidationTests|FullyQualifiedName~ExecutionWorkItemWorkerHostedServiceTests|FullyQualifiedName~ExecutionOwnershipRecoveryHostedServiceTests|FullyQualifiedName~RuntimeServiceCollectionExtensionsTests|FullyQualifiedName~ExecutionTenantIdLogMessagesTests' -f xml -o "$coverageXml"
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] test / coverage failed'
        exit 1
    }

    dotnet sonarscanner end /d:sonar.token="$($env:SONAR_TOKEN)"
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonarscanner end failed'
        exit 1
    }
}
finally {
    Pop-Location
}

Write-Host '[OK] SonarQube analysis completed.'
