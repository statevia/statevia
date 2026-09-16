-- TenantId UUID を Loki Org（X-Scope-OrgID）へ写す。欠落・非 JSON は statevia-ops。
-- JsonConsole はプレースホルダを State.tenantId / State.TenantId に置く。
-- IncludeScopes 時は Scopes 配列にも TenantId が付く（Engine 行などテンプレートに無い場合）。
-- Docker fluentd の tag（compose サービス名）を service ラベル用に残す。

local ops = "statevia-ops"
local uuid_pat = "^%x%x%x%x%x%x%x%x%-%x%x%x%x%-%x%x%x%x%-%x%x%x%x%-%x%x%x%x%x%x%x%x%x%x%x%x$"

local function is_uuid(value)
    if type(value) ~= "string" then
        return false
    end
    return string.match(string.lower(value), uuid_pat) ~= nil
end

local function from_table(tbl)
    if type(tbl) ~= "table" then
        return nil
    end
    return tbl["tenantId"] or tbl["TenantId"]
end

local function from_scopes(scopes)
    if type(scopes) ~= "table" then
        return nil
    end
    for _, scope in pairs(scopes) do
        local tenant_id = from_table(scope)
        if tenant_id ~= nil then
            return tenant_id
        end
    end
    return nil
end

function set_org_id(tag, timestamp, record)
    local tenant_id = from_table(record["State"])
    if tenant_id == nil then
        tenant_id = record["tenantId"] or record["TenantId"]
    end
    if tenant_id == nil then
        tenant_id = from_scopes(record["Scopes"])
    end

    if is_uuid(tenant_id) then
        record["org_id"] = tenant_id
    else
        record["org_id"] = ops
    end

    record["service"] = tag

    return 1, timestamp, record
end
