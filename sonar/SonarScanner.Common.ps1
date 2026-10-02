#Requires -Version 5.1
<#
.SYNOPSIS
  SonarScanner 共通の接続パラメータを組み立てる。

.DESCRIPTION
  各 sonar-scanner-*.ps1 からドットソースする。
  既定の送信先は SonarQube Cloud（組織 statevia、https://sonarcloud.io）。
  ローカル Community Build へ送るときは SONAR_HOST_URL に http://localhost:9000 を指定する。
  Cloud 以外のホストでは sonar.organization を付けない。
  SONAR_QUALITYGATE_WAIT=true のときだけ Quality Gate の完了を待ち、失敗なら終了コードを非 0 にする。

.NOTES
  ファイル先頭の実行コードは置かない。ドットソース時に解析が走らないようにする。
#>

function Get-StateviaSonarHostUrl {
    <#
    .SYNOPSIS
      解析の送信先 URL を返す。
    .DESCRIPTION
      SONAR_HOST_URL が空なら https://sonarcloud.io。末尾のスラッシュは除く。
    #>
    if ([string]::IsNullOrWhiteSpace($env:SONAR_HOST_URL)) {
        return 'https://sonarcloud.io'
    }

    return $env:SONAR_HOST_URL.Trim().TrimEnd('/')
}

function Get-StateviaSonarOrganization {
    <#
    .SYNOPSIS
      SonarQube Cloud の組織キーを返す。
    .DESCRIPTION
      送信先が sonarcloud.io のときだけ組織キーを返す。それ以外は $null。
      SONAR_ORGANIZATION が空なら statevia。
    #>
    $hostUrl = Get-StateviaSonarHostUrl
    if ($hostUrl -notmatch 'sonarcloud\.io') {
        return $null
    }

    if ([string]::IsNullOrWhiteSpace($env:SONAR_ORGANIZATION)) {
        return 'statevia'
    }

    return $env:SONAR_ORGANIZATION.Trim()
}

function Get-StateviaSonarBeginArguments {
    <#
    .SYNOPSIS
      dotnet sonarscanner の begin 以降の引数を返す。
    .PARAMETER ProjectKey
      SonarQube Cloud の projectKey。
    .PARAMETER RepositoryRoot
      sonar.projectBaseDir に渡すリポジトリルート。
    .PARAMETER CoverageReportPath
      VS Coverage XML のパス。空ならカバレッジ引数を付けない。
    .PARAMETER AnalysisExclusions
      sonar.exclusions。カンマ区切り。
    .PARAMETER CoverageExclusions
      sonar.coverage.exclusions。カンマ区切り。
    .PARAMETER Inclusions
      sonar.inclusions。カンマ区切り。
    .PARAMETER CpdExclusions
      sonar.cpd.exclusions。カンマ区切り。
    .PARAMETER ExcludeTestProjects
      sonar.dotnet.excludeTestProjects=true を付ける。
    .OUTPUTS
      string[]。先頭は begin。
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectKey,

        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [string] $CoverageReportPath,
        [string] $AnalysisExclusions,
        [string] $CoverageExclusions,
        [string] $Inclusions,
        [string] $CpdExclusions,
        [switch] $ExcludeTestProjects
    )

    if ([string]::IsNullOrWhiteSpace($env:SONAR_TOKEN)) {
        Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    }

    $arguments = [System.Collections.Generic.List[string]]::new()
    $arguments.Add('begin')
    $arguments.Add("/k:$ProjectKey")
    $arguments.Add("/d:sonar.host.url=$(Get-StateviaSonarHostUrl)")
    $arguments.Add("/d:sonar.token=$($env:SONAR_TOKEN)")
    $arguments.Add("/d:sonar.projectBaseDir=$RepositoryRoot")
    $arguments.Add('/d:sonar.scanner.scanAll=false')

    $organization = Get-StateviaSonarOrganization
    if (-not [string]::IsNullOrWhiteSpace($organization)) {
        # Scanner for .NET 11 は sonar.organization プロパティを拒否し、/o: を要求する。
        $arguments.Add("/o:$organization")
    }

    if ($ExcludeTestProjects) {
        $arguments.Add('/d:sonar.dotnet.excludeTestProjects=true')
    }

    if (-not [string]::IsNullOrWhiteSpace($CoverageReportPath)) {
        $arguments.Add("/d:sonar.cs.vscoveragexml.reportsPaths=$CoverageReportPath")
    }

    if (-not [string]::IsNullOrWhiteSpace($Inclusions)) {
        $arguments.Add("/d:sonar.inclusions=$Inclusions")
    }

    if (-not [string]::IsNullOrWhiteSpace($AnalysisExclusions)) {
        $arguments.Add("/d:sonar.exclusions=$AnalysisExclusions")
    }

    if (-not [string]::IsNullOrWhiteSpace($CoverageExclusions)) {
        $arguments.Add("/d:sonar.coverage.exclusions=$CoverageExclusions")
    }

    if (-not [string]::IsNullOrWhiteSpace($CpdExclusions)) {
        $arguments.Add("/d:sonar.cpd.exclusions=$CpdExclusions")
    }

    if ($env:SONAR_QUALITYGATE_WAIT -eq 'true') {
        $arguments.Add('/d:sonar.qualitygate.wait=true')
        $arguments.Add('/d:sonar.qualitygate.timeout=300')
    }

    # 配列を 1 個の戻り値として返す（パイプラインで要素に分解されないようにする）。
    return ,$arguments.ToArray()
}

function Get-StateviaSonarUiScannerArguments {
    <#
    .SYNOPSIS
      UI 用 npx sonar-scanner の引数を返す。
    .PARAMETER ProjectKey
      SonarQube Cloud の projectKey。
    .OUTPUTS
      string[]。先頭は --yes。
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectKey
    )

    if ([string]::IsNullOrWhiteSpace($env:SONAR_TOKEN)) {
        Write-Error '環境変数 SONAR_TOKEN が設定されていません。'
    }

    $arguments = [System.Collections.Generic.List[string]]::new()
    $arguments.Add('--yes')
    $arguments.Add('sonar-scanner')
    $arguments.Add("-Dsonar.projectKey=$ProjectKey")
    $arguments.Add("-Dsonar.host.url=$(Get-StateviaSonarHostUrl)")
    $arguments.Add("-Dsonar.token=$($env:SONAR_TOKEN)")

    $organization = Get-StateviaSonarOrganization
    if (-not [string]::IsNullOrWhiteSpace($organization)) {
        $arguments.Add("-Dsonar.organization=$organization")
    }

    if ($env:SONAR_QUALITYGATE_WAIT -eq 'true') {
        $arguments.Add('-Dsonar.qualitygate.wait=true')
        $arguments.Add('-Dsonar.qualitygate.timeout=300')
    }

    return ,$arguments.ToArray()
}
