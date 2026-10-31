#Requires -Version 5.1
<#
.SYNOPSIS
  GitHub Actions で解析する Sonar プロジェクトを変更パスから選ぶ。

.DESCRIPTION
  push では 7 件すべてを選ぶ。
  pull_request では変更ファイルに対応するコンポーネントだけを選ぶ。
  sonar/ と、dotnet/ の build/・Directory.Build.*・Directory.Packages.props・global.json・.editorconfig の変更は全件を選ぶ。
  結果は GITHUB_OUTPUT の has_work と matrix（JSON 配列）へ書く。
  対象が 0 件のときは has_work=false とし、空配列でワークフローが落ちないようプレースホルダを matrix に入れる。

.NOTES
  pull_request では環境変数 SONAR_DIFF_BASE と SONAR_DIFF_HEAD にコミット SHA を渡す。
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    Write-Error '環境変数 GITHUB_OUTPUT が設定されていません。'
    exit 1
}

$projectCatalog = @(
    @{
        Id = 'engine'
        Script = './sonar/sonar-scanner-engine.ps1'
        Node = $false
        Prefixes = @('dotnet/core/engine/')
    }
    @{
        Id = 'api'
        Script = './sonar/sonar-scanner-api.ps1'
        Node = $false
        Prefixes = @('dotnet/service/api/', 'dotnet/core/application/', 'dotnet/core/actions/', 'dotnet/infrastructure/')
    }
    @{
        Id = 'runtime'
        Script = './sonar/sonar-scanner-runtime.ps1'
        Node = $false
        Prefixes = @('dotnet/service/runtime/')
    }
    @{
        Id = 'cli'
        Script = './sonar/sonar-scanner-cli.ps1'
        Node = $false
        Prefixes = @('dotnet/service/cli/')
    }
    @{
        Id = 'action-host'
        Script = './sonar/sonar-scanner-action-host.ps1'
        Node = $false
        Prefixes = @('dotnet/service/action-host/')
    }
    @{
        Id = 'reference'
        Script = './sonar/sonar-scanner-reference.ps1'
        Node = $false
        Prefixes = @('dotnet/modules/reference/')
    }
    @{
        Id = 'ui'
        Script = './sonar/sonar-scanner-ui.ps1'
        Node = $true
        Prefixes = @('nodejs/studio/')
    }
)

$sharedPrefixes = @(
    'sonar/',
    'dotnet/build/',
    'dotnet/Directory.Build.',
    'dotnet/Directory.Packages.props',
    'dotnet/global.json',
    'dotnet/.editorconfig',
    '.github/workflows/sonar.yml'
)

function Test-PathMatch {
    <#
    .SYNOPSIS
      変更パスのいずれかが接頭辞に一致するかを返す。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $Paths,

        [Parameter(Mandatory = $true)]
        [string[]] $Prefixes
    )

    foreach ($path in $Paths) {
        $normalized = ($path -replace '\\', '/').TrimStart('./')
        foreach ($prefix in $Prefixes) {
            if ($normalized.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
    }

    return $false
}

$runAll = $env:GITHUB_EVENT_NAME -ne 'pull_request'
$changedPaths = @()

if (-not $runAll) {
    if ([string]::IsNullOrWhiteSpace($env:SONAR_DIFF_BASE) -or [string]::IsNullOrWhiteSpace($env:SONAR_DIFF_HEAD)) {
        Write-Error 'pull_request では SONAR_DIFF_BASE と SONAR_DIFF_HEAD が必要です。'
        exit 1
    }

    $diffOutput = git diff --name-only $env:SONAR_DIFF_BASE $env:SONAR_DIFF_HEAD
    if ($LASTEXITCODE -ne 0) {
        Write-Error 'git diff に失敗しました。'
        exit 1
    }

    $changedPaths = @($diffOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if (Test-PathMatch -Paths $changedPaths -Prefixes $sharedPrefixes) {
        $runAll = $true
    }
}

$selected = [System.Collections.Generic.List[object]]::new()
foreach ($project in $projectCatalog) {
    $include = $runAll -or (Test-PathMatch -Paths $changedPaths -Prefixes $project.Prefixes)
    if ($include) {
        $selected.Add([ordered]@{
                id     = $project.Id
                script = $project.Script
                node   = [bool]$project.Node
            })
    }
}

$hasWork = $selected.Count -gt 0
if (-not $hasWork) {
    $selected.Add([ordered]@{
            id     = 'none'
            script = ''
            node   = $false
        })
}

$matrixJson = if ($selected.Count -eq 1) {
    '[' + ($selected[0] | ConvertTo-Json -Compress) + ']'
}
else {
    ($selected | ConvertTo-Json -Compress)
}

$hasWorkText = if ($hasWork) { 'true' } else { 'false' }
Add-Content -Path $env:GITHUB_OUTPUT -Value "has_work=$hasWorkText"
Add-Content -Path $env:GITHUB_OUTPUT -Value "matrix=$matrixJson"

Write-Host "Sonar projects selected: $hasWorkText"
Write-Host $matrixJson
