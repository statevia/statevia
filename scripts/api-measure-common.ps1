#Requires -Version 7.0
# 計測スクリプト共通（TAT / RPS）。エントリから dot-source する。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ProbeYaml = @'
version: 1

workflow:
  id: tat.probe.noop
  name: ApiTatProbe
  description: Best-effort non-execution API TAT probe.

nodes:
  - name: flow.start
    type: start
    label: start
    next: flow.noop

  - name: flow.noop
    type: action
    label: noop
    action: statevia.action.builtin.execution.noop
    next: flow.end

  - name: flow.end
    type: end
    label: end
'@

$script:State = [ordered]@{
    RunId              = ('r{0}' -f [guid]::NewGuid().ToString('N').Substring(0, 10))
    AccessToken        = ''
    DefinitionReadId   = ''
    DefinitionMutateId = ''
    CreatedDefinitionIds = [System.Collections.Generic.List[string]]::new()
    GroupId            = ''
    ServiceAccountId   = ''
    LastApiKeyId       = ''
    CreatedApiKeyIds   = [System.Collections.Generic.List[string]]::new()
    SeedUserId         = ''
    CreatedUserIds     = [System.Collections.Generic.List[string]]::new()
}

function Get-ResolvedPassword {
    if (-not [string]::IsNullOrWhiteSpace($Password)) {
        return $Password
    }

    foreach ($name in @('STATEVIA_API_TAT_PASSWORD', 'STATEVIA_CAPACITY_PASSWORD')) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return ''
}

function Get-GitSha {
    try {
        $sha = & git rev-parse --short HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($sha)) {
            return $sha.Trim()
        }
    }
    catch {
        return ''
    }

    return ''
}

function Get-NearestRankPercentile {
    param(
        [double[]] $Samples,
        [double] $Percentile
    )

    if ($null -eq $Samples -or $Samples.Count -eq 0) {
        return $null
    }

    $sorted = $Samples | Sort-Object
    if ($Percentile -eq 0) {
        return [double]$sorted[0]
    }

    $rank = [int][math]::Ceiling($Percentile / 100.0 * $sorted.Count) - 1
    $rank = [math]::Clamp($rank, 0, $sorted.Count - 1)
    return [double]$sorted[$rank]
}

function New-StringList {
    param([string[]] $Items = @())

    $list = [System.Collections.Generic.List[string]]::new()
    foreach ($item in @($Items)) {
        if ($null -ne $item) {
            $list.Add($item)
        }
    }

    # 空の List が $null に潰されないようにする。
    return , $list
}

function ConvertTo-JsonBody {
    param([hashtable] $Payload)

    $object = [System.Text.Json.Nodes.JsonObject]::new()
    foreach ($key in $Payload.Keys) {
        $value = $Payload[$key]
        if ($null -eq $value) {
            $object[$key] = $null
            continue
        }

        if ($value -is [bool]) {
            $object[$key] = [System.Text.Json.Nodes.JsonValue]::Create([bool]$value)
            continue
        }

        if ($value -is [string]) {
            $object[$key] = [System.Text.Json.Nodes.JsonValue]::Create([string]$value)
            continue
        }

        if ($value -is [System.Collections.IEnumerable] -and $value -isnot [string]) {
            $array = [System.Text.Json.Nodes.JsonArray]::new()
            foreach ($item in $value) {
                if ($item -is [bool]) {
                    [void]$array.Add([System.Text.Json.Nodes.JsonValue]::Create([bool]$item))
                }
                else {
                    [void]$array.Add([System.Text.Json.Nodes.JsonValue]::Create([string]$item))
                }
            }
            $object[$key] = $array
            continue
        }

        $object[$key] = [System.Text.Json.Nodes.JsonValue]::Create([string]$value)
    }

    return $object.ToJsonString()
}

function New-HttpClient {
    param(
        [string] $ResolvedBaseUrl,
        [int] $RequestTimeoutSeconds,
        [int] $MaxConnectionsPerServer = 16
    )

    $builder = [UriBuilder]::new($ResolvedBaseUrl)
    if (-not $builder.Path.EndsWith('/')) {
        $builder.Path += '/'
    }

    $handler = [System.Net.Http.SocketsHttpHandler]::new()
    $handler.MaxConnectionsPerServer = [Math]::Max(2, $MaxConnectionsPerServer)
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.BaseAddress = $builder.Uri
    $client.Timeout = [TimeSpan]::FromSeconds($RequestTimeoutSeconds)
    return $client
}

function Invoke-TatHttp {
    param(
        [Parameter(Mandatory)]
        [System.Net.Http.HttpClient] $Client,
        [Parameter(Mandatory)]
        [string] $Method,
        [Parameter(Mandatory)]
        [string] $Path,
        [string] $BodyJson = '',
        [switch] $Authenticated,
        [switch] $Idempotent,
        [switch] $OmitTenant
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method),
        $Path)

    if (-not $OmitTenant) {
        $request.Headers.TryAddWithoutValidation('X-Tenant-Id', $Tenant) | Out-Null
    }

    if ($Authenticated) {
        if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
            $request.Headers.TryAddWithoutValidation('X-Api-Key', $ApiKey) | Out-Null
        }
        elseif (-not [string]::IsNullOrWhiteSpace($script:State.AccessToken)) {
            $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
                'Bearer',
                $script:State.AccessToken)
        }
    }

    if ($Idempotent) {
        $request.Headers.TryAddWithoutValidation('X-Idempotency-Key', [guid]::NewGuid().ToString('N')) | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($BodyJson)) {
        $request.Content = [System.Net.Http.StringContent]::new(
            $BodyJson,
            [System.Text.Encoding]::UTF8,
            'application/json')
    }

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $response = $Client.Send($request)
        try {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $watch.Stop()
            return [pscustomobject]@{
                StatusCode = [int]$response.StatusCode
                ElapsedMs  = [math]::Round($watch.Elapsed.TotalMilliseconds, 3)
                Body       = $content
                Error      = ''
            }
        }
        finally {
            $response.Dispose()
        }
    }
    catch {
        $watch.Stop()
        return [pscustomobject]@{
            StatusCode = 0
            ElapsedMs  = [math]::Round($watch.Elapsed.TotalMilliseconds, 3)
            Body       = ''
            Error      = $_.Exception.Message
        }
    }
    finally {
        $request.Dispose()
    }
}

function ConvertFrom-JsonObject {
    param([string] $Json)

    if ([string]::IsNullOrWhiteSpace($Json)) {
        return $null
    }

    return ($Json | ConvertFrom-Json -Depth 8)
}

function Get-ProbeCatalog {
    return @(
        [pscustomobject]@{
            Id            = 'health.get'
            Method        = 'GET'
            PathTemplate  = '/v1/health'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @()
            Group         = 'health.get'
            Expected      = @(200)
            Authenticated = $false
            Idempotent    = $false
            OmitTenant    = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/health' -OmitTenant
            }
        }
        [pscustomobject]@{
            Id            = 'auth.login'
            Method        = 'POST'
            PathTemplate  = '/v1/auth/login'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Password')
            Group         = 'auth.login'
            Expected      = @(200)
            Authenticated = $false
            Idempotent    = $false
            OmitTenant    = $true
            Invoke        = {
                param($Client)
                $resolvedPassword = Get-ResolvedPassword
                $body = ConvertTo-JsonBody @{
                    tenantKey = $Tenant
                    username  = $Username
                    password  = $resolvedPassword
                }
                Invoke-TatHttp -Client $Client -Method POST -Path 'v1/auth/login' -BodyJson $body -OmitTenant
            }
        }
        [pscustomobject]@{
            Id            = 'auth.me.get'
            Method        = 'GET'
            PathTemplate  = '/v1/auth/me'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal')
            Group         = 'auth.me.get'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/auth/me' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'definitions.validate'
            Method        = 'POST'
            PathTemplate  = '/v1/definitions/validate'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal')
            Group         = 'definitions.validate'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                $body = ConvertTo-JsonBody @{
                    name = 'tat.validate'
                    yaml = $script:ProbeYaml
                }
                Invoke-TatHttp -Client $Client -Method POST -Path 'v1/definitions/validate' -BodyJson $body -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'definitions.list'
            Method        = 'GET'
            PathTemplate  = '/v1/definitions?limit={limit}'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal')
            Group         = 'definitions.list'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path ("v1/definitions?limit={0}" -f $DefinitionListLimit) -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'definitions.get'
            Method        = 'GET'
            PathTemplate  = '/v1/definitions/{id}'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal', 'Definition')
            Group         = 'definitions.get'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path ("v1/definitions/{0}" -f $script:State.DefinitionReadId) -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'definitions.schema.nodes'
            Method        = 'GET'
            PathTemplate  = '/v1/definitions/schema/nodes'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal')
            Group         = 'definitions.schema.nodes'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/definitions/schema/nodes' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'actions.schema.list'
            Method        = 'GET'
            PathTemplate  = '/v1/actions/schema'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal')
            Group         = 'actions.schema.list'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/actions/schema' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'actions.schema.index'
            Method        = 'GET'
            PathTemplate  = '/v1/actions/schema/index'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal')
            Group         = 'actions.schema.index'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/actions/schema/index' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'actions.schema.get'
            Method        = 'GET'
            PathTemplate  = '/v1/actions/schema/{actionId}'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal')
            Group         = 'actions.schema.get'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                $encoded = [Uri]::EscapeDataString($ActionId)
                Invoke-TatHttp -Client $Client -Method GET -Path ("v1/actions/schema/{0}" -f $encoded) -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'graphs.get'
            Method        = 'GET'
            PathTemplate  = '/v1/graphs/{graphId}'
            Suites        = @('Read', 'Default', 'Admin', 'All')
            Needs         = @('Principal', 'Definition')
            Group         = 'graphs.get'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path ("v1/graphs/{0}" -f $script:State.DefinitionReadId) -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'definitions.create'
            Method        = 'POST'
            PathTemplate  = '/v1/definitions'
            Suites        = @('Default', 'Admin', 'All')
            Needs         = @('Principal')
            Group         = 'definitions.create'
            Expected      = @(201)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $name = 'tat.{0}.c.{1}' -f $script:State.RunId, [guid]::NewGuid().ToString('N').Substring(0, 8)
                $body = ConvertTo-JsonBody @{
                    name = $name
                    yaml = $script:ProbeYaml
                }
                $result = Invoke-TatHttp -Client $Client -Method POST -Path 'v1/definitions' -BodyJson $body -Authenticated -Idempotent
                $parsed = ConvertFrom-JsonObject $result.Body
                if ($parsed -and $parsed.displayId) {
                    $script:State.CreatedDefinitionIds.Add([string]$parsed.displayId)
                }
                $result
            }
        }
        [pscustomobject]@{
            Id            = 'definitions.update'
            Method        = 'PUT'
            PathTemplate  = '/v1/definitions/{id}'
            Suites        = @('Default', 'Admin', 'All')
            Needs         = @('Principal', 'Definition')
            Group         = 'definitions.update'
            Expected      = @(200)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $body = ConvertTo-JsonBody @{
                    name = 'tat.{0}.mutate' -f $script:State.RunId
                    yaml = $script:ProbeYaml
                }
                Invoke-TatHttp -Client $Client -Method PUT -Path ("v1/definitions/{0}" -f $script:State.DefinitionMutateId) -BodyJson $body -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'definitions.delete'
            Method        = 'DELETE'
            PathTemplate  = '/v1/definitions/{id}'
            Suites        = @('Default', 'Admin', 'All')
            Needs         = @('Principal', 'Definition')
            Group         = 'definitions.lifecycle'
            Expected      = @(204)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method DELETE -Path ("v1/definitions/{0}" -f $script:State.DefinitionMutateId) -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'definitions.restore'
            Method        = 'POST'
            PathTemplate  = '/v1/definitions/{id}/restore'
            Suites        = @('Default', 'Admin', 'All')
            Needs         = @('Principal', 'Definition')
            Group         = 'definitions.lifecycle'
            Expected      = @(200)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method POST -Path ("v1/definitions/{0}/restore" -f $script:State.DefinitionMutateId) -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'admin.permissions.list'
            Method        = 'GET'
            PathTemplate  = '/v1/admin/permissions'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal')
            Group         = 'admin.permissions.list'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/admin/permissions' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'admin.users.list'
            Method        = 'GET'
            PathTemplate  = '/v1/admin/users'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal')
            Group         = 'admin.users.list'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/admin/users' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'admin.groups.list'
            Method        = 'GET'
            PathTemplate  = '/v1/admin/groups'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal')
            Group         = 'admin.groups.list'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/admin/groups' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'admin.groups.get'
            Method        = 'GET'
            PathTemplate  = '/v1/admin/groups/{groupId}'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal', 'Group')
            Group         = 'admin.groups.get'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path ("v1/admin/groups/{0}" -f $script:State.GroupId) -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'admin.api-keys.list'
            Method        = 'GET'
            PathTemplate  = '/v1/admin/api-keys'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal')
            Group         = 'admin.api-keys.list'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/admin/api-keys' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'admin.service-accounts.list'
            Method        = 'GET'
            PathTemplate  = '/v1/admin/service-accounts'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal')
            Group         = 'admin.service-accounts.list'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/admin/service-accounts' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'admin.service-accounts.get'
            Method        = 'GET'
            PathTemplate  = '/v1/admin/service-accounts/{serviceAccountId}'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal', 'ServiceAccount')
            Group         = 'admin.service-accounts.get'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path ("v1/admin/service-accounts/{0}" -f $script:State.ServiceAccountId) -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'admin.modules.list'
            Method        = 'GET'
            PathTemplate  = '/v1/admin/modules'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal')
            Group         = 'admin.modules.list'
            Expected      = @(200)
            Authenticated = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method GET -Path 'v1/admin/modules' -Authenticated
            }
        }
        [pscustomobject]@{
            Id            = 'admin.groups.members.put'
            Method        = 'PUT'
            PathTemplate  = '/v1/admin/groups/{groupId}/members'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal', 'Group')
            Group         = 'admin.groups.members.put'
            Expected      = @(200)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $userIds = New-StringList
                if (-not [string]::IsNullOrWhiteSpace($script:State.SeedUserId)) {
                    $userIds = New-StringList @($script:State.SeedUserId)
                }
                $body = ConvertTo-JsonBody @{ userIds = $userIds }
                Invoke-TatHttp -Client $Client -Method PUT -Path ("v1/admin/groups/{0}/members" -f $script:State.GroupId) -BodyJson $body -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'admin.groups.permissions.put'
            Method        = 'PUT'
            PathTemplate  = '/v1/admin/groups/{groupId}/permissions'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal', 'Group')
            Group         = 'admin.groups.permissions.put'
            Expected      = @(200)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $body = ConvertTo-JsonBody @{ permissionKeys = (New-StringList @('definitions.read')) }
                Invoke-TatHttp -Client $Client -Method PUT -Path ("v1/admin/groups/{0}/permissions" -f $script:State.GroupId) -BodyJson $body -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'admin.api-keys.create'
            Method        = 'POST'
            PathTemplate  = '/v1/admin/api-keys'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal')
            Group         = 'admin.api-keys.lifecycle'
            Expected      = @(201)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $body = ConvertTo-JsonBody @{
                    name          = ('Tat Probe Key {0}' -f [guid]::NewGuid().ToString('N').Substring(0, 8))
                    allowedScopes = (New-StringList @('definitions.read'))
                }
                $result = Invoke-TatHttp -Client $Client -Method POST -Path 'v1/admin/api-keys' -BodyJson $body -Authenticated -Idempotent
                $parsed = ConvertFrom-JsonObject $result.Body
                if ($parsed -and $parsed.apiKeyId) {
                    $script:State.LastApiKeyId = [string]$parsed.apiKeyId
                    $script:State.CreatedApiKeyIds.Add([string]$parsed.apiKeyId)
                }
                $result
            }
        }
        [pscustomobject]@{
            Id            = 'admin.api-keys.revoke'
            Method        = 'DELETE'
            PathTemplate  = '/v1/admin/api-keys/{apiKeyId}'
            Suites        = @('Admin', 'All')
            Needs         = @('Principal')
            Group         = 'admin.api-keys.lifecycle'
            Expected      = @(204)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                if ([string]::IsNullOrWhiteSpace($script:State.LastApiKeyId)) {
                    return [pscustomobject]@{
                        StatusCode = 0
                        ElapsedMs  = 0
                        Body       = ''
                        Error      = 'apiKeyId is missing (create did not succeed).'
                    }
                }

                $id = $script:State.LastApiKeyId
                $script:State.LastApiKeyId = ''
                Invoke-TatHttp -Client $Client -Method DELETE -Path ("v1/admin/api-keys/{0}" -f $id) -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'admin.users.create'
            Method        = 'POST'
            PathTemplate  = '/v1/admin/users'
            Suites        = @('All')
            Needs         = @('Principal')
            Group         = 'admin.users.create'
            Expected      = @(201)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
                $body = ConvertTo-JsonBody @{
                    username    = ('tatp{0}' -f $suffix)
                    password    = ('TatP{0}' -f [guid]::NewGuid().ToString('N').Substring(0, 12))
                    displayName = ('Tat Probe User {0}' -f $suffix)
                }
                $result = Invoke-TatHttp -Client $Client -Method POST -Path 'v1/admin/users' -BodyJson $body -Authenticated -Idempotent
                $parsed = ConvertFrom-JsonObject $result.Body
                if ($parsed -and $parsed.userId) {
                    $script:State.CreatedUserIds.Add([string]$parsed.userId)
                }
                $result
            }
        }
        [pscustomobject]@{
            Id            = 'admin.groups.create'
            Method        = 'POST'
            PathTemplate  = '/v1/admin/groups'
            Suites        = @('All')
            Needs         = @('Principal')
            Group         = 'admin.groups.create'
            Expected      = @(201)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $body = ConvertTo-JsonBody @{
                    name = ('Tat Probe Group {0}' -f [guid]::NewGuid().ToString('N').Substring(0, 8))
                }
                Invoke-TatHttp -Client $Client -Method POST -Path 'v1/admin/groups' -BodyJson $body -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'admin.service-accounts.create'
            Method        = 'POST'
            PathTemplate  = '/v1/admin/service-accounts'
            Suites        = @('All')
            Needs         = @('Principal', 'Group')
            Group         = 'admin.service-accounts.create'
            Expected      = @(201)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $body = ConvertTo-JsonBody @{
                    name     = ('Tat Probe SA {0}' -f [guid]::NewGuid().ToString('N').Substring(0, 8))
                    groupIds = (New-StringList @($script:State.GroupId))
                }
                Invoke-TatHttp -Client $Client -Method POST -Path 'v1/admin/service-accounts' -BodyJson $body -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'admin.users.patch'
            Method        = 'PATCH'
            PathTemplate  = '/v1/admin/users/{userId}'
            Suites        = @('All')
            Needs         = @('Principal', 'PatchUser')
            Group         = 'admin.users.patch'
            Expected      = @(200)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                $body = ConvertTo-JsonBody @{ isActive = $true }
                Invoke-TatHttp -Client $Client -Method PATCH -Path ("v1/admin/users/{0}" -f $script:State.SeedUserId) -BodyJson $body -Authenticated -Idempotent
            }
        }
        [pscustomobject]@{
            Id            = 'internal.modules.reload'
            Method        = 'POST'
            PathTemplate  = '/internal/modules/reload'
            Suites        = @('All')
            Needs         = @('Principal')
            Group         = 'internal.modules.reload'
            Expected      = @(204)
            Authenticated = $true
            Idempotent    = $true
            Invoke        = {
                param($Client)
                Invoke-TatHttp -Client $Client -Method POST -Path 'internal/modules/reload' -Authenticated -Idempotent
            }
        }
    )
}

function Resolve-SelectedProbes {
    param([object[]] $Catalog)

    $requested = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($requested.Count -gt 0) {
        $unknown = $requested | Where-Object { $id = $_; -not ($Catalog.Id -contains $id) }
        if ($unknown) {
            throw ("Unknown probe id: {0}" -f ($unknown -join ', '))
        }

        return @($Catalog | Where-Object { $requested -contains $_.Id })
    }

    return @($Catalog | Where-Object { $_.Suites -contains $Suite })
}

function Assert-Health {
    param([System.Net.Http.HttpClient] $Client)

    $result = Invoke-TatHttp -Client $Client -Method GET -Path 'v1/health' -OmitTenant
    if ($result.StatusCode -ne 200) {
        throw ("GET /v1/health returned {0}. Start Service API first." -f $result.StatusCode)
    }
}

function Connect-Principal {
    param(
        [System.Net.Http.HttpClient] $Client,
        [string] $ResolvedPassword
    )

    if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
        return
    }

    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $script:State.AccessToken = $Token
        return
    }

    if ([string]::IsNullOrWhiteSpace($ResolvedPassword)) {
        throw 'Specify -Password, STATEVIA_API_TAT_PASSWORD / STATEVIA_CAPACITY_PASSWORD, -Token, or -ApiKey.'
    }

    $body = ConvertTo-JsonBody @{
        tenantKey = $Tenant
        username  = $Username
        password  = $ResolvedPassword
    }
    $result = Invoke-TatHttp -Client $Client -Method POST -Path 'v1/auth/login' -BodyJson $body -OmitTenant
    if ($result.StatusCode -ne 200) {
        throw ("POST /v1/auth/login returned {0}." -f $result.StatusCode)
    }

    $parsed = ConvertFrom-JsonObject $result.Body
    if (-not $parsed -or [string]::IsNullOrWhiteSpace($parsed.accessToken)) {
        throw 'POST /v1/auth/login did not return accessToken.'
    }

    $script:State.AccessToken = [string]$parsed.accessToken
}

function New-ProbeDefinition {
    param(
        [System.Net.Http.HttpClient] $Client,
        [string] $Name
    )

    $body = ConvertTo-JsonBody @{
        name = $Name
        yaml = $script:ProbeYaml
    }
    $result = Invoke-TatHttp -Client $Client -Method POST -Path 'v1/definitions' -BodyJson $body -Authenticated -Idempotent
    if ($result.StatusCode -ne 201) {
        throw ("POST /v1/definitions ({0}) returned {1}." -f $Name, $result.StatusCode)
    }

    $parsed = ConvertFrom-JsonObject $result.Body
    if (-not $parsed -or [string]::IsNullOrWhiteSpace($parsed.displayId)) {
        throw ("POST /v1/definitions ({0}) did not return displayId." -f $Name)
    }

    $id = [string]$parsed.displayId
    $script:State.CreatedDefinitionIds.Add($id)
    return $id
}

function Initialize-SeedResources {
    param(
        [System.Net.Http.HttpClient] $Client,
        [object[]] $Selected
    )

    $needs = @($Selected | ForEach-Object { $_.Needs } | Select-Object -Unique)

    if ($needs -contains 'Definition') {
        $prefix = 'tat.{0}' -f $script:State.RunId
        $script:State.DefinitionReadId = New-ProbeDefinition -Client $Client -Name ('{0}.read' -f $prefix)
        $mutateIds = @('definitions.update', 'definitions.delete', 'definitions.restore')
        $selectedIds = @($Selected | ForEach-Object { $_.Id })
        if ($selectedIds | Where-Object { $mutateIds -contains $_ }) {
            $script:State.DefinitionMutateId = New-ProbeDefinition -Client $Client -Name ('{0}.mutate' -f $prefix)
        }
        else {
            $script:State.DefinitionMutateId = $script:State.DefinitionReadId
        }
    }

    if ($needs -contains 'Group' -or $needs -contains 'ServiceAccount' -or $needs -contains 'PatchUser') {
        # 既存グループの members / permissions を上書きしないよう、計測専用グループを作る。
        $body = ConvertTo-JsonBody @{ name = ('Tat Probe Seed {0}' -f $script:State.RunId) }
        $created = Invoke-TatHttp -Client $Client -Method POST -Path 'v1/admin/groups' -BodyJson $body -Authenticated -Idempotent
        if ($created.StatusCode -ne 201) {
            throw ("POST /v1/admin/groups returned {0}." -f $created.StatusCode)
        }

        $parsedGroup = ConvertFrom-JsonObject $created.Body
        $script:State.GroupId = [string]$parsedGroup.groupId
        Write-Warning '削除 API が無いため、計測用グループはテナントに残ります。'
    }

    if ($needs -contains 'ServiceAccount') {
        $accounts = Invoke-TatHttp -Client $Client -Method GET -Path 'v1/admin/service-accounts' -Authenticated
        if ($accounts.StatusCode -eq 200) {
            $parsedAccounts = ConvertFrom-JsonObject $accounts.Body
            $firstAccount = @($parsedAccounts) | Select-Object -First 1
            if ($firstAccount -and $firstAccount.serviceAccountId) {
                $script:State.ServiceAccountId = [string]$firstAccount.serviceAccountId
            }
        }

        if ([string]::IsNullOrWhiteSpace($script:State.ServiceAccountId)) {
            if ([string]::IsNullOrWhiteSpace($script:State.GroupId)) {
                throw 'GET /v1/admin/service-accounts/{id} には既存 ServiceAccount か、作成用グループが必要です。'
            }

            $body = ConvertTo-JsonBody @{
                name     = ('Tat Probe Seed SA {0}' -f $script:State.RunId)
                groupIds = (New-StringList @($script:State.GroupId))
            }
            $created = Invoke-TatHttp -Client $Client -Method POST -Path 'v1/admin/service-accounts' -BodyJson $body -Authenticated -Idempotent
            if ($created.StatusCode -ne 201) {
                throw ("POST /v1/admin/service-accounts returned {0}." -f $created.StatusCode)
            }

            $parsedAccount = ConvertFrom-JsonObject $created.Body
            $script:State.ServiceAccountId = [string]$parsedAccount.serviceAccountId
            Write-Warning '削除 API が無いため、計測用 ServiceAccount はテナントに残ります。'
        }
    }

    if ($needs -contains 'PatchUser') {
        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $body = ConvertTo-JsonBody @{
            username    = ('tatp{0}' -f $suffix)
            password    = ('TatP{0}' -f [guid]::NewGuid().ToString('N').Substring(0, 12))
            displayName = ('Tat Probe Patch {0}' -f $suffix)
        }
        $created = Invoke-TatHttp -Client $Client -Method POST -Path 'v1/admin/users' -BodyJson $body -Authenticated -Idempotent
        if ($created.StatusCode -ne 201) {
            throw ("POST /v1/admin/users (seed) returned {0}." -f $created.StatusCode)
        }

        $parsedUser = ConvertFrom-JsonObject $created.Body
        $script:State.SeedUserId = [string]$parsedUser.userId
        $script:State.CreatedUserIds.Add($script:State.SeedUserId)
        Write-Warning '削除 API が無いため、計測用ユーザーはテナントに残ります。'
    }
}

function Invoke-ProbeAttempt {
    param(
        [System.Net.Http.HttpClient] $Client,
        [object] $Probe
    )

    & $Probe.Invoke $Client
}

function Measure-ProbeGroup {
    param(
        [System.Net.Http.HttpClient] $Client,
        [object[]] $GroupProbes
    )

    # DELETE→RESTORE や create→revoke は、同一反復内で順番を保つ。
    for ($i = 0; $i -lt $Warmup; $i++) {
        foreach ($probe in $GroupProbes) {
            Invoke-ProbeAttempt -Client $Client -Probe $probe | Out-Null
        }
    }

    $stats = [ordered]@{}
    foreach ($probe in $GroupProbes) {
        $stats[$probe.Id] = @{
            Probe        = $probe
            SuccessMs    = [System.Collections.Generic.List[double]]::new()
            Ok           = 0
            Error        = 0
            LastError    = ''
            StatusCounts = @{}
        }
    }

    for ($i = 0; $i -lt $Iterations; $i++) {
        foreach ($probe in $GroupProbes) {
            $result = Invoke-ProbeAttempt -Client $Client -Probe $probe
            $row = $stats[$probe.Id]
            $codeKey = [string]$result.StatusCode
            if (-not $row.StatusCounts.ContainsKey($codeKey)) {
                $row.StatusCounts[$codeKey] = 0
            }
            $row.StatusCounts[$codeKey] += 1

            $expected = @($probe.Expected)
            if ($expected -contains $result.StatusCode) {
                $row.Ok += 1
                $row.SuccessMs.Add([double]$result.ElapsedMs)
            }
            else {
                $row.Error += 1
                if ([string]::IsNullOrWhiteSpace($row.LastError)) {
                    if ($result.Error) {
                        $row.LastError = $result.Error
                    }
                    else {
                        $row.LastError = "HTTP $($result.StatusCode)"
                    }
                }
            }
        }
    }

    $rows = @()
    foreach ($probe in $GroupProbes) {
        $row = $stats[$probe.Id]
        $samples = @($row.SuccessMs)
        $mean = $null
        if ($samples.Count -gt 0) {
            $mean = [math]::Round((($samples | Measure-Object -Average).Average), 3)
        }

        $rows += [pscustomobject]@{
            id           = $probe.Id
            method       = $probe.Method
            path         = $probe.PathTemplate
            iterations   = $Iterations
            ok           = $row.Ok
            error        = $row.Error
            minMs        = if ($samples.Count -gt 0) { [math]::Round(($samples | Measure-Object -Minimum).Minimum, 3) } else { $null }
            p50Ms        = Get-NearestRankPercentile -Samples $samples -Percentile 50
            p95Ms        = Get-NearestRankPercentile -Samples $samples -Percentile 95
            maxMs        = if ($samples.Count -gt 0) { [math]::Round(($samples | Measure-Object -Maximum).Maximum, 3) } else { $null }
            meanMs       = $mean
            statusCounts = $row.StatusCounts
            lastError    = $row.LastError
        }
    }

    return $rows
}

function Clear-SeedResources {
    param([System.Net.Http.HttpClient] $Client)

    if ($SkipCleanup) {
        Write-Host 'SkipCleanup: 定義と API キーは残します。'
        return
    }

    foreach ($id in @($script:State.CreatedApiKeyIds)) {
        if ([string]::IsNullOrWhiteSpace($id)) {
            continue
        }

        Invoke-TatHttp -Client $Client -Method DELETE -Path ("v1/admin/api-keys/{0}" -f $id) -Authenticated -Idempotent | Out-Null
    }

    foreach ($id in @($script:State.CreatedDefinitionIds)) {
        if ([string]::IsNullOrWhiteSpace($id)) {
            continue
        }

        Invoke-TatHttp -Client $Client -Method DELETE -Path ("v1/definitions/{0}" -f $id) -Authenticated -Idempotent | Out-Null
    }
}

function Write-ResultFiles {
    param(
        [object] $Report,
        [string] $Path,
        [string[]] $CsvProperties = @(
            'id', 'method', 'path', 'iterations', 'ok', 'error', 'minMs', 'p50Ms', 'p95Ms', 'maxMs', 'meanMs', 'lastError'
        )
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $base = $Path
    if ($base -match '\.(json|csv)$') {
        $base = $base.Substring(0, $base.Length - 5)
    }

    $jsonPath = "$base.json"
    $csvPath = "$base.csv"
    $Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8
    $Report.probes |
        Select-Object -Property $CsvProperties |
        Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding utf8
    Write-Host ("Wrote {0}" -f $jsonPath)
    Write-Host ("Wrote {0}" -f $csvPath)
}

function Test-ProbeRpsSafe {
    param([object] $Probe)

    if ($Probe.Method -eq 'GET') {
        return $true
    }

    return $Probe.Id -in @('auth.login', 'definitions.validate')
}

function Get-FrozenRpsCall {
    param([object] $Probe)

    switch ($Probe.Id) {
        'health.get' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/health'
                BodyJson      = ''
                Authenticated = $false
                Idempotent    = $false
                OmitTenant    = $true
            }
        }
        'auth.login' {
            return [pscustomobject]@{
                Method        = 'POST'
                Path          = 'v1/auth/login'
                BodyJson      = (ConvertTo-JsonBody @{
                        tenantKey = $Tenant
                        username  = $Username
                        password  = (Get-ResolvedPassword)
                    })
                Authenticated = $false
                Idempotent    = $false
                OmitTenant    = $true
            }
        }
        'auth.me.get' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/auth/me'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'definitions.validate' {
            return [pscustomobject]@{
                Method        = 'POST'
                Path          = 'v1/definitions/validate'
                BodyJson      = (ConvertTo-JsonBody @{
                        name = 'tat.validate'
                        yaml = $script:ProbeYaml
                    })
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'definitions.list' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = ('v1/definitions?limit={0}' -f $DefinitionListLimit)
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'definitions.get' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = ('v1/definitions/{0}' -f $script:State.DefinitionReadId)
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'definitions.schema.nodes' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/definitions/schema/nodes'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'actions.schema.list' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/actions/schema'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'actions.schema.index' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/actions/schema/index'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'actions.schema.get' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = ('v1/actions/schema/{0}' -f [Uri]::EscapeDataString($ActionId))
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'graphs.get' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = ('v1/graphs/{0}' -f $script:State.DefinitionReadId)
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'admin.permissions.list' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/admin/permissions'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'admin.users.list' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/admin/users'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'admin.groups.list' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/admin/groups'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'admin.groups.get' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = ('v1/admin/groups/{0}' -f $script:State.GroupId)
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'admin.api-keys.list' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/admin/api-keys'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'admin.service-accounts.list' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/admin/service-accounts'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'admin.service-accounts.get' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = ('v1/admin/service-accounts/{0}' -f $script:State.ServiceAccountId)
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        'admin.modules.list' {
            return [pscustomobject]@{
                Method        = 'GET'
                Path          = 'v1/admin/modules'
                BodyJson      = ''
                Authenticated = $true
                Idempotent    = $false
                OmitTenant    = $false
            }
        }
        default {
            throw ("RPS 用の固定リクエストが未定義です: {0}" -f $Probe.Id)
        }
    }
}

function Measure-ProbeRps {
    param(
        [System.Net.Http.HttpClient] $Client,
        [object] $Probe,
        [int] $Concurrency,
        [int] $DurationSeconds,
        [int] $WarmupSeconds
    )

    $frozen = Get-FrozenRpsCall -Probe $Probe
    $probeCheck = Invoke-TatHttp -Client $Client -Method $frozen.Method -Path $frozen.Path -BodyJson $frozen.BodyJson `
        -Authenticated:$frozen.Authenticated -Idempotent:$frozen.Idempotent -OmitTenant:$frozen.OmitTenant
    if (@($Probe.Expected) -notcontains $probeCheck.StatusCode) {
        throw ("{0} の事前確認が HTTP {1} でした。" -f $Probe.Id, $probeCheck.StatusCode)
    }

    $successMs = [System.Collections.Concurrent.ConcurrentBag[double]]::new()
    $errorMarks = [System.Collections.Concurrent.ConcurrentBag[int]]::new()
    $errors = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
    $statusCounts = [System.Collections.Concurrent.ConcurrentDictionary[string, int]]::new()
    $expected = @($Probe.Expected)
    $recordFrom = [DateTime]::UtcNow.AddSeconds([Math]::Max(0, $WarmupSeconds))
    $deadline = $recordFrom.AddSeconds($DurationSeconds)
    $token = [string]$script:State.AccessToken
    $apiKeyValue = [string]$ApiKey
    $tenantValue = [string]$Tenant

    $pool = [runspacefactory]::CreateRunspacePool(1, $Concurrency)
    $pool.Open()
    $workers = @()
    try {
        for ($i = 0; $i -lt $Concurrency; $i++) {
            $ps = [powershell]::Create()
            $ps.RunspacePool = $pool
            [void]$ps.AddScript({
                    param(
                        $Client,
                        $Frozen,
                        $RecordFrom,
                        $Deadline,
                        $Expected,
                        $SuccessMs,
                        $ErrorMarks,
                        $Errors,
                        $StatusCounts,
                        $TenantValue,
                        $Token,
                        $ApiKeyValue
                    )

                    while ([DateTime]::UtcNow -lt $Deadline) {
                        $request = [System.Net.Http.HttpRequestMessage]::new(
                            [System.Net.Http.HttpMethod]::new($Frozen.Method),
                            $Frozen.Path)
                        try {
                            if (-not $Frozen.OmitTenant) {
                                $request.Headers.TryAddWithoutValidation('X-Tenant-Id', $TenantValue) | Out-Null
                            }

                            if ($Frozen.Authenticated) {
                                if (-not [string]::IsNullOrWhiteSpace($ApiKeyValue)) {
                                    $request.Headers.TryAddWithoutValidation('X-Api-Key', $ApiKeyValue) | Out-Null
                                }
                                elseif (-not [string]::IsNullOrWhiteSpace($Token)) {
                                    $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
                                }
                            }

                            if ($Frozen.Idempotent) {
                                $request.Headers.TryAddWithoutValidation('X-Idempotency-Key', [guid]::NewGuid().ToString('N')) | Out-Null
                            }

                            if (-not [string]::IsNullOrWhiteSpace($Frozen.BodyJson)) {
                                $request.Content = [System.Net.Http.StringContent]::new(
                                    $Frozen.BodyJson,
                                    [System.Text.Encoding]::UTF8,
                                    'application/json')
                            }

                            $watch = [System.Diagnostics.Stopwatch]::StartNew()
                            try {
                                $response = $Client.Send($request)
                                try {
                                    [void]$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                                    $watch.Stop()
                                    $statusCode = [int]$response.StatusCode
                                    $elapsedMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 3)
                                    $errorText = ''
                                }
                                finally {
                                    $response.Dispose()
                                }
                            }
                            catch {
                                $watch.Stop()
                                $statusCode = 0
                                $elapsedMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 3)
                                $errorText = $_.Exception.Message
                            }
                        }
                        finally {
                            $request.Dispose()
                        }

                        if ([DateTime]::UtcNow -lt $RecordFrom) {
                            continue
                        }

                        $StatusCounts.AddOrUpdate([string]$statusCode, 1, { param($key, $current) $current + 1 }) | Out-Null
                        if ($Expected -contains $statusCode) {
                            $SuccessMs.Add([double]$elapsedMs)
                        }
                        else {
                            $ErrorMarks.Add(1)
                            if ($Errors.Count -eq 0) {
                                if (-not [string]::IsNullOrWhiteSpace($errorText)) {
                                    $Errors.Add($errorText)
                                }
                                else {
                                    $Errors.Add("HTTP $statusCode")
                                }
                            }
                        }
                    }
                }).AddArgument($Client).AddArgument($frozen).AddArgument($recordFrom).AddArgument($deadline).AddArgument($expected).
                AddArgument($successMs).AddArgument($errorMarks).AddArgument($errors).AddArgument($statusCounts).
                AddArgument($tenantValue).AddArgument($token).AddArgument($apiKeyValue)

            $workers += [pscustomobject]@{
                PowerShell = $ps
                Handle     = $ps.BeginInvoke()
            }
        }

        foreach ($worker in $workers) {
            $worker.PowerShell.EndInvoke($worker.Handle)
            if ($worker.PowerShell.HadErrors) {
                foreach ($streamError in $worker.PowerShell.Streams.Error) {
                    $errors.Add([string]$streamError)
                }
            }
            $worker.PowerShell.Dispose()
        }
    }
    finally {
        $pool.Close()
        $pool.Dispose()
    }

    $elapsedSeconds = [Math]::Max($DurationSeconds, 0.001)
    $ok = $successMs.Count
    $err = $errorMarks.Count
    $samples = @($successMs)
    $mean = $null
    if ($samples.Count -gt 0) {
        $mean = [math]::Round((($samples | Measure-Object -Average).Average), 3)
    }

    $lastError = ''
    if ($errors.Count -gt 0) {
        $lastError = @($errors)[0]
    }

    $statusSnapshot = @{}
    foreach ($pair in $statusCounts.GetEnumerator()) {
        $statusSnapshot[$pair.Key] = $pair.Value
    }

    return [pscustomobject]@{
        id           = $Probe.Id
        method       = $Probe.Method
        path         = $Probe.PathTemplate
        concurrency  = $Concurrency
        durationSec  = $DurationSeconds
        elapsedSec   = [math]::Round($elapsedSeconds, 3)
        ok           = $ok
        error        = $err
        successRps   = [math]::Round(($ok / $elapsedSeconds), 2)
        attemptRps   = [math]::Round((($ok + $err) / $elapsedSeconds), 2)
        minMs        = if ($samples.Count -gt 0) { [math]::Round(($samples | Measure-Object -Minimum).Minimum, 3) } else { $null }
        p50Ms        = Get-NearestRankPercentile -Samples $samples -Percentile 50
        p95Ms        = Get-NearestRankPercentile -Samples $samples -Percentile 95
        maxMs        = if ($samples.Count -gt 0) { [math]::Round(($samples | Measure-Object -Maximum).Maximum, 3) } else { $null }
        meanMs       = $mean
        statusCounts = $statusSnapshot
        lastError    = $lastError
    }
}
