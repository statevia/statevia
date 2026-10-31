#Requires -Version 5.1
<#
.SYNOPSIS
  engine 向け SonarScanner（begin → build → coverage → end）を実行する。

.DESCRIPTION
  スクリプト配置（リポジトリの sonar/）から engine とカバレッジ出力パスを解決する。
  カレントディレクトリに依存しない。
  sonar.projectBaseDir は dotnet/（ファイルのキーを集約前と同じに保つ）。Scanner for .NET の scanAll（既定 true）は、MSBuild のプロジェクト外のファイル（Dockerfile や設定）も解析に混ぜるため、オフにする。
  build / test に /p:StateviaSonarScope=engine を渡し、担当外プロジェクトを SonarQubeExclude する。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
  送信先は SonarScanner.Common.ps1（既定は SonarQube Cloud）。
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SonarScanner.Common.ps1')

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Get-StateviaRepositoryRoot
$dotnetRoot = Join-Path $repoRoot 'dotnet'
$engineDir = Join-Path $dotnetRoot 'core\engine'
$coverageXml = Join-Path (Get-StateviaSonarOutputDirectory) 'core-engine-coverage.xml'

# Engine sln 内の *.Tests と他コンポーネントを除外
$sonarAnalysisExclusions = @(
    '**/service/**',
    '**/infrastructure/**',
    '**/ui/**',
    '**/infrastructure/**',
    '**/samples/**',
    '**/docker-compose.yml',
    '**/*.Tests/**'
) -join ','
$sonarCoverageExclusions = $sonarAnalysisExclusions

if (-not (Test-Path -LiteralPath $engineDir -PathType Container)) {
    Write-Error "dotnet/core/engine ディレクトリが見つかりません: $engineDir"
    exit 1
}

Push-Location -LiteralPath $engineDir
try {
    $beginArguments = Get-StateviaSonarBeginArguments `
        -ProjectKey 'statevia_statevia_engine' `
        -ProjectBaseDir $dotnetRoot `
        -CoverageReportPath $coverageXml `
        -AnalysisExclusions $sonarAnalysisExclusions `
        -CoverageExclusions $sonarCoverageExclusions `
        -ExcludeTestProjects
    dotnet sonarscanner @beginArguments
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] sonarscanner begin failed'
        exit 1
    }

    dotnet build 'statevia-engine.sln' '/p:StateviaSonarScope=engine'
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] build failed'
        exit 1
    }

    dotnet-coverage collect 'dotnet test /p:StateviaSonarScope=engine' -f xml -o "$coverageXml"
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
