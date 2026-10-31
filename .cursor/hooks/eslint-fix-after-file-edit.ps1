#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
  # .cursor/hooks/eslint-fix-after-file-edit.ps1 -> .cursor/hooks -> .cursor -> repo root
  $cursorDir = Split-Path -Parent $PSScriptRoot
  return (Split-Path -Parent $cursorDir)
}

$rawPayload = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($rawPayload)) {
  exit 0
}

$repoRoot = Get-RepositoryRoot
$studioRoot = Join-Path $repoRoot 'nodejs\studio'
$lockPath = Join-Path $PSScriptRoot '.eslint-fix-after-file-edit.lock'
$minSecondsBetweenRuns = 45

if (Test-Path -LiteralPath $lockPath) {
  $ageSeconds = (New-TimeSpan -Start (Get-Item -LiteralPath $lockPath).LastWriteTime -End (Get-Date)).TotalSeconds
  if ($ageSeconds -lt $minSecondsBetweenRuns) {
    exit 0
  }
}

$matchedPaths = New-Object System.Collections.Generic.HashSet[string]

function Add-MatchedPath([string]$candidate) {
  if ([string]::IsNullOrWhiteSpace($candidate)) {
    return
  }
  if ($candidate -notmatch '(?i)\.(tsx|ts)$') {
    return
  }
  if ($candidate -notmatch '(?i)nodejs[\\/]studio[\\/]') {
    return
  }
  [void]$matchedPaths.Add($candidate)
}

try {
  $payload = $rawPayload | ConvertFrom-Json
  foreach ($name in @('file_path', 'filePath', 'path')) {
    $property = $payload.PSObject.Properties[$name]
    if ($null -ne $property) {
      Add-MatchedPath -candidate ([string]$property.Value)
    }
  }
} catch {
  # 生テキストのフォールバックへ進む。
}

$decodedPayload = $rawPayload -replace '\\\\', '\'
$pathPattern = '(?i)(?:[A-Za-z]:\\|/)?(?:[^"\s]*?[\\/])?nodejs[\\/]studio[\\/][^"\s]+?\.(?:tsx|ts)\b'
foreach ($match in [regex]::Matches($decodedPayload, $pathPattern)) {
  Add-MatchedPath -candidate $match.Value.Trim('"', "'", ',', ']')
}

if ($matchedPaths.Count -eq 0) {
  exit 0
}

$eslintTargets = New-Object System.Collections.Generic.List[string]
foreach ($candidate in $matchedPaths) {
  $normalized = $candidate -replace '/', '\'
  if ($normalized -match '\\node_modules\\|\\\.next\\|\\dist\\|\\coverage\\') {
    continue
  }
  if (-not [System.IO.Path]::IsPathRooted($normalized)) {
    $normalized = Join-Path $repoRoot $normalized
  }
  if (-not (Test-Path -LiteralPath $normalized)) {
    continue
  }
  $fullPath = (Resolve-Path -LiteralPath $normalized).Path
  if (-not $fullPath.StartsWith($studioRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    continue
  }
  $eslintTargets.Add($fullPath)
}

if ($eslintTargets.Count -eq 0) {
  exit 0
}

Set-Content -LiteralPath $lockPath -Value ("{0:o}" -f (Get-Date)) -Encoding UTF8

Write-Host "afterFileEdit: eslint --fix ($($eslintTargets.Count) files)" -ForegroundColor Cyan

Push-Location $studioRoot
try {
  & npx --no-install eslint --fix @($eslintTargets.ToArray())
  if ($LASTEXITCODE -ne 0) {
    Write-Host "afterFileEdit: eslint --fix left issues (exit $LASTEXITCODE). npm run lint still enforces them." -ForegroundColor Yellow
  }
} catch {
  Write-Host "afterFileEdit: eslint --fix skipped: $($_.Exception.Message)" -ForegroundColor Yellow
} finally {
  Pop-Location
}

exit 0
