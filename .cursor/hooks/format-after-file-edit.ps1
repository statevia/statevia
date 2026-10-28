#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
  # .cursor/hooks/format-after-file-edit.ps1 -> .cursor/hooks -> .cursor -> repo root
  $cursorDir = Split-Path -Parent $PSScriptRoot
  return (Split-Path -Parent $cursorDir)
}

function Try-ExtractSolutionsFromRawPayload([string]$rawPayload) {
  $selected = [System.Collections.Generic.HashSet[string]]::new()

  # JSON payload typically includes edited file paths; we match common repository segments.
  # Identifier must be a sln that actually contains the edited project (engine sln is Engine only).
  if ($rawPayload -match 'core[\\/]+engine[\\/]+') { [void]$selected.Add('engine') }
  if ($rawPayload -match 'core[\\/]+application[\\/]+') { [void]$selected.Add('api') }
  if ($rawPayload -match 'core[\\/]+actions[\\/]+') { [void]$selected.Add('infrastructure') }
  if ($rawPayload -match 'service[\\/]+api[\\/]+') { [void]$selected.Add('api') }
  if ($rawPayload -match 'service[\\/]+runtime[\\/]+') { [void]$selected.Add('api') }
  if ($rawPayload -match 'service[\\/]+action-host[\\/]+') { [void]$selected.Add('action-host') }
  if ($rawPayload -match 'service[\\/]+cli[\\/]+') { [void]$selected.Add('cli') }
  if ($rawPayload -match 'infrastructure[\\/]+') { [void]$selected.Add('infrastructure') }
  if ($rawPayload -match 'modules[\\/]+reference[\\/]+') { [void]$selected.Add('reference') }
  if ($rawPayload -match 'tests[\\/]+Statevia\.Architecture\.Tests[\\/]+') { [void]$selected.Add('api') }

  # Ensure sln-level edits are also handled.
  if ($rawPayload -match 'statevia-engine\.sln') { [void]$selected.Add('engine') }
  if ($rawPayload -match 'statevia-api\.sln') { [void]$selected.Add('api') }
  if ($rawPayload -match 'statevia-action-host\.sln') { [void]$selected.Add('action-host') }
  if ($rawPayload -match 'statevia-cli\.sln') { [void]$selected.Add('cli') }
  if ($rawPayload -match 'statevia-infrastructure\.sln') { [void]$selected.Add('infrastructure') }
  if ($rawPayload -match 'statevia-reference\.sln') { [void]$selected.Add('reference') }

  return @($selected)
}

function ShouldSkipBecauseRecentlyFormatted([string]$lockPath, [int]$minSecondsBetweenRuns) {
  if (-not (Test-Path -LiteralPath $lockPath)) {
    return $false
  }

  $ageSeconds = (New-TimeSpan -Start (Get-Item -LiteralPath $lockPath).LastWriteTime -End (Get-Date)).TotalSeconds
  return ($ageSeconds -lt $minSecondsBetweenRuns)
}

$rawPayload = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($rawPayload)) {
  $rawPayload = '{}'
}

$repoRoot = Get-RepositoryRoot

$lockPath = Join-Path $PSScriptRoot '.format-after-file-edit.lock'
$minSecondsBetweenRuns = 45

if (ShouldSkipBecauseRecentlyFormatted -lockPath $lockPath -minSecondsBetweenRuns $minSecondsBetweenRuns) {
  return
}

# Touch the lock so rapid consecutive edits debounce correctly.
Set-Content -LiteralPath $lockPath -Value ("{0:o}" -f (Get-Date)) -Encoding UTF8

$solutions = Try-ExtractSolutionsFromRawPayload -rawPayload $rawPayload
if ($solutions.Count -eq 0) {
  # If payload does not include edited paths (or we cannot extract them), skip to avoid wasteful formatting.
  return
}

$formatScriptPath = Join-Path $repoRoot 'sonar\dotnet-format-all.ps1'
if (-not (Test-Path -LiteralPath $formatScriptPath)) {
  throw "dotnet format script not found: $formatScriptPath"
}

Write-Host "afterFileEdit: running whitespace + using-cleanup ($($solutions -join ', '))" -ForegroundColor Cyan

foreach ($solution in $solutions) {
  # Minimize work (verbosity=minimal) while still applying formatting changes.
  & $formatScriptPath -Solution $solution -Verbosity 'minimal'
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet format failed: solution=$solution"
  }
}

