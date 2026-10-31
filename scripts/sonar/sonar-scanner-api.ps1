#Requires -Version 5.1
<#
.SYNOPSIS
  api 向け SonarScanner（begin → build → coverage → end）を実行する。

.DESCRIPTION
  スクリプト配置（リポジトリの scripts/sonar/）から api とカバレッジ出力パスを解決する。
  カレントディレクトリに依存しない。
  解析・カバレッジ除外は dotnet/service/api/coverage.runsettings の意図（Engine / Program / Migrations）に合わせる。
  誤ってリポジトリルート等から実行した場合に UI / engine / リファレンス Module が混ざらないよう、nodejs/studio・core/engine・modules/reference も除外する。
  sonar.projectBaseDir は dotnet/（ファイルのキーを集約前と同じに保つ）。Scanner for .NET の scanAll（既定 true）は、MSBuild のプロジェクト外のファイル（Dockerfile や設定）も解析に混ぜるため、オフにする。
  依存プロジェクトの Roslyn protobuf は sonar.exclusions では消えないため、build / test に /p:StateviaSonarScope=api を渡し SonarQubeExclude する。
  Program.cs は解析対象に残し、カバレッジ分母だけ除外する。

.NOTES
  環境変数 SONAR_TOKEN を事前に設定すること。
  送信先は SonarScanner.Common.ps1（既定は SonarQube Cloud）。
  sonar-project.properties は SonarScanner for .NET では使わない（begin の /d: で指定）。
  STATEVIA_SKIP_SCENARIO_TESTS=true のとき、Category=Scenario を dotnet test から除外する。
  この環境変数を読むのは scripts/test-api.ps1 だけなので、スキャナ側でも同じフィルタを付ける。
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SonarScanner.Common.ps1')

# dotnet/service/api/coverage.runsettings の Exclude / ExcludeByFile と整合
# sonar.projectBaseDir を dotnet/ に固定し、ファイルのキー（service/api/... 等）を集約前と同じに保つ
# Program.cs を sonar.exclusions に入れると、コンパイル済み protobuf と analysis context が食い違う。
$sonarAnalysisExclusions = @(
    '**/nodejs/studio/**',
    '**/core/engine/**',
    '**/engine/**',
    '**/service/cli/**',
    '**/service/action-host/**',
    '**/service/runtime/**',
    '**/modules/reference/**',
    '**/Statevia.Core.Engine/**',
    '**/Migrations/**',
    '**/Dockerfile',
    '**/*.Tests/**'
) -join ','
$sonarCoverageExclusions = @(
    $sonarAnalysisExclusions,
    '**/Program.cs'
) -join ','

if (-not $env:SONAR_TOKEN) {
    Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    exit 1
}

$repoRoot = Get-StateviaRepositoryRoot
$dotnetRoot = Join-Path $repoRoot 'dotnet'
$apiDir = Join-Path $dotnetRoot 'service\api'
$coverageXml = Join-Path (Get-StateviaSonarOutputDirectory) 'service-api-coverage.xml'

if (-not (Test-Path -LiteralPath $apiDir -PathType Container)) {
    Write-Error "dotnet/service/api ディレクトリが見つかりません: $apiDir"
    exit 1
}

Push-Location -LiteralPath $apiDir
try {
    $beginArguments = Get-StateviaSonarBeginArguments `
        -ProjectKey 'statevia_statevia_api' `
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

    dotnet build 'statevia-api.sln' '/p:StateviaSonarScope=api'
    if ($LASTEXITCODE -ne 0) {
        Write-Error '[ERROR] build failed'
        exit 1
    }

    $apiTestCommand = 'dotnet test /p:StateviaSonarScope=api'
    if ($env:STATEVIA_SKIP_SCENARIO_TESTS -eq 'true') {
        $apiTestCommand += ' --filter Category!=Scenario'
        Write-Host 'シナリオテストを除外してカバレッジを取得します（Category!=Scenario）'
    }

    dotnet-coverage collect $apiTestCommand -f xml -o "$coverageXml"
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
