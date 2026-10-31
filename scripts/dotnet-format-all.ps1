#Requires -Version 5.1
<#
.SYNOPSIS
  リポジトリ内の .NET コードに、インデント・空白・不要な using のクリーンアップを適用する。

.DESCRIPTION
  スクリプト配置（リポジトリの scripts/）から dotnet/ と各 sln のパスを解決する。
  カレントディレクトリに依存しない。
  `whitespace` と `style --diagnostics IDE0005 IDE0065` を実行し、
  インデント・空白・不要な using の削除、using 配置の整理に限定する。

  -Solution 未指定時は dotnet/ 全体を一括で処理する（CI もこのモード）。
  - whitespace: `--folder` で dotnet/ の .cs をまとめて処理する。
    MSBuild のワークスペース読み込みが不要なため速い。
    gitignore 対象のうち .cs を含むもの（bin / obj、modules/local/）は除外する。
  - style: IDE 用 `statevia.sln` を 1 回だけ読み込む。
    `statevia.sln` に含まれない Scenario.Tests は個別の csproj として追加で処理する。
  コンポーネント別 sln を個別に処理すると、共通プロジェクトの restore と読み込みが sln ごとに繰り返されて遅い。

  -Solution 指定時は、そのコンポーネント別 sln（dotnet/ からの相対パス）だけに whitespace と style を順に実行する。
  - core/engine/statevia-engine.sln
  - service/api/statevia-api.sln
  - service/action-host/statevia-action-host.sln
  - service/cli/statevia-cli.sln
  - infrastructure/statevia-infrastructure.sln
  - modules/reference/statevia-reference.sln

.PARAMETER VerifyNoChanges
  書式変更が発生しないことを検証する（`dotnet format --verify-no-changes`）。
  CI や PR 前チェック向け。

.PARAMETER Verbosity
  `dotnet format` の詳細レベル（quiet / minimal / normal / detailed / diagnostic）。

.PARAMETER Solution
  対象 sln の識別子（`engine` / `api` / `action-host` / `cli` / `infrastructure` / `reference`）、
  dotnet/ からの相対パス、またはファイル名（例: statevia-api.sln）。未指定時は dotnet/ 全体。

.EXAMPLE
  .\scripts\dotnet-format-all.ps1

.EXAMPLE
  .\scripts\dotnet-format-all.ps1 -VerifyNoChanges

.EXAMPLE
  .\scripts\dotnet-format-all.ps1 -Solution statevia-api.sln
#>
[CmdletBinding()]
param(
    [switch]$VerifyNoChanges,
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'normal',
    [string]$Solution
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetRoot = Join-Path $repoRoot 'dotnet'

$allSolutions = @(
    @{ Name = 'engine'; RelativePath = 'core/engine/statevia-engine.sln' }
    @{ Name = 'api'; RelativePath = 'service/api/statevia-api.sln' }
    @{ Name = 'action-host'; RelativePath = 'service/action-host/statevia-action-host.sln' }
    @{ Name = 'cli'; RelativePath = 'service/cli/statevia-cli.sln' }
    @{ Name = 'infrastructure'; RelativePath = 'infrastructure/statevia-infrastructure.sln' }
    @{ Name = 'reference'; RelativePath = 'modules/reference/statevia-reference.sln' }
)

# 全体モードで whitespace --folder から除外するパス（dotnet/ からの相対）。gitignore 対象で .cs を含むもの。
$folderExcludes = @('modules/local', '**/bin', '**/obj')

# 全体モードで style を掛ける workspace（dotnet/ からの相対）。statevia.sln に含まれないプロジェクトはここへ足す。
$repoStyleWorkspaces = @(
    'statevia.sln'
    'service/api/Statevia.Service.Api.Scenario.Tests/Statevia.Service.Api.Scenario.Tests.csproj'
)

function Resolve-RepoPath {
    <#
    .SYNOPSIS
      dotnet/ からの相対パスを絶対パスへ変換する。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    Join-Path $dotnetRoot ($RelativePath -replace '[\\/]', [System.IO.Path]::DirectorySeparatorChar)
}

function New-StyleArgs {
    <#
    .SYNOPSIS
      using の整理に限定した `dotnet format style` の引数を返す。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string] $WorkspacePath
    )

    @('format', 'style', $WorkspacePath, '--diagnostics', 'IDE0005', 'IDE0065', '--severity', 'info', '--verbosity', $Verbosity)
}

# 各ターゲットは Name と、順に実行する dotnet format コマンド（Commands）を持つ。
$targets = @(if ([string]::IsNullOrWhiteSpace($Solution)) {
    $missing = @($repoStyleWorkspaces | Where-Object { -not (Test-Path -LiteralPath (Resolve-RepoPath $_) -PathType Leaf) })
    if ($missing.Count -gt 0) {
        Write-Error ("style の対象が見つかりません: {0}" -f ($missing -join ', '))
        exit 1
    }

    $excludeArgs = @($folderExcludes | ForEach-Object { '--exclude', $_ })
    $repoCommands = @(
        @{
            Name = 'whitespace'
            Args = @('format', 'whitespace', $dotnetRoot, '--folder') + $excludeArgs + @('--verbosity', $Verbosity)
        }
    ) + @($repoStyleWorkspaces | ForEach-Object {
        @{
            Name = "using-cleanup ($_)"
            Args = New-StyleArgs -WorkspacePath (Resolve-RepoPath $_)
        }
    })

    @(@{ Name = 'repository'; Commands = $repoCommands })
} else {
    $matched = $allSolutions | Where-Object {
        $_.Name -eq $Solution -or
        $_.RelativePath -eq $Solution -or
        [System.IO.Path]::GetFileName($_.RelativePath) -eq $Solution
    }
    if (-not $matched) {
        $available = ($allSolutions | ForEach-Object { [System.IO.Path]::GetFileName($_.RelativePath) }) -join ', '
        Write-Error "ソリューション '$Solution' が見つかりません。利用可能: $available"
        exit 1
    }

    @($matched | ForEach-Object {
        $solutionPath = Resolve-RepoPath $_.RelativePath
        if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
            Write-Error "ソリューションが見つかりません: $solutionPath"
            exit 1
        }

        @{
            Name = "$($_.Name) ($($_.RelativePath))"
            Commands = @(
                @{
                    Name = 'whitespace'
                    Args = @('format', 'whitespace', $solutionPath, '--verbosity', $Verbosity)
                }
                @{
                    Name = 'using-cleanup'
                    Args = New-StyleArgs -WorkspacePath $solutionPath
                }
            )
        }
    })
})

Write-Host "dotnet format:$($targets.Count) target(s)" -ForegroundColor Cyan
Write-Host "Root: $dotnetRoot"
if ($VerifyNoChanges) {
    Write-Host 'Mode: verify-no-changes' -ForegroundColor Yellow
}

$failed = [System.Collections.Generic.List[string]]::new()

foreach ($target in $targets) {
    Write-Host ""
    Write-Host "=== $($target.Name) ===" -ForegroundColor Cyan

    foreach ($formatCommand in $target.Commands) {
        $formatArgs = if ($VerifyNoChanges) { $formatCommand.Args + '--verify-no-changes' } else { $formatCommand.Args }

        Write-Host ("--- {0}" -f $formatCommand.Name) -ForegroundColor DarkCyan
        & dotnet @($formatArgs)
        if ($LASTEXITCODE -ne 0) {
            $failed.Add("$($target.Name): $($formatCommand.Name)")
            break
        }
    }
}

Write-Host ""
if ($failed.Count -gt 0) {
    Write-Error ("dotnet format failed: {0}" -f ($failed -join ', '))
    exit 1
}

Write-Host 'dotnet format completed.' -ForegroundColor Green
