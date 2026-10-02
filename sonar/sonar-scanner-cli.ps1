#Requires -Version 5.1
<#
.SYNOPSIS
  cli 向け SonarScanner（begin → build → coverage → end）を実行する。

.DESCRIPTION
  スクリプト配置（リポジトリの sonar/）から cli とカバレッジ出力パスを解決する。
  カレントディレクトリに依存しない。
  依存アセンブリ（engine / api / ui / infrastructure）が混ざらないよう解析・カバレッジ除外を設定する。
  sonar.projectBaseDir がリポジトリルートのため、Scanner for .NET の scanAll（既定 true）が ui/studio を JS/TS 解析に混ぜる。scanAll はオフにする。
  依存プロジェクトの Roslyn protobuf は sonar.exclusions では消えないため、build / test に /p:StateviaSonarScope=cli を渡し SonarQubeExclude する。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
  送信先は SonarScanner.Common.ps1（既定は SonarQube Cloud）。
  sonar-project.properties は SonarScanner for .NET では使わない（begin の /d: で指定）。
  プロジェクトキー: statevia_statevia_cli
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SonarScanner.Common.ps1')

$sonarAnalysisExclusions = @(
    '**/service/api/**',
    '**/api/**',
    '**/ui/studio/**',
    '**/core/engine/**',
    '**/engine/**',
    '**/Statevia.Core.Engine/**',
    '**/Statevia.Service.Api/**',
    '**/Statevia.Actions.Abstractions/**',
    '**/service/runtime/**',
    '**/infrastructure/**',
    '**/Migrations/**',
    '**/docker-compose.yml'
) -join ','
$sonarCoverageExclusions = $sonarAnalysisExclusions

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$cliDir = Join-Path $repoRoot 'service\cli'
$coverageXml = Join-Path $PSScriptRoot 'service-cli-coverage.xml'

if (-not (Test-Path -LiteralPath $cliDir -PathType Container)) {
    Write-Error "service/cli ディレクトリが見つかりません: $cliDir"
    exit 1
}

Push-Location -LiteralPath $cliDir
try {
    $beginArguments = Get-StateviaSonarBeginArguments `
        -ProjectKey 'statevia_statevia_cli' `
        -RepositoryRoot $repoRoot `
        -CoverageReportPath $coverageXml `
        -AnalysisExclusions $sonarAnalysisExclusions `
        -CoverageExclusions $sonarCoverageExclusions
    dotnet sonarscanner @beginArguments
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonarscanner begin failed'
        exit 1
    }

    dotnet build 'statevia-cli.sln' '/p:StateviaSonarScope=cli'
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] build failed'
        exit 1
    }

    dotnet-coverage collect 'dotnet test /p:StateviaSonarScope=cli' -f xml -o "$coverageXml"
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
