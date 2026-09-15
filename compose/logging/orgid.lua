-- TenantId UUID を Loki Org（X-Scope-OrgID）へ写す。欠落・非 JSON は statevia-ops。
-- JsonConsole のキーは State.tenantId / State.TenantId / トップレベルを見る。
-- Docker fluentd の tag（compose サービス名）を service ラベル用に残す。

function set_org_id(tag, timestamp, record)
    local ops = "statevia-ops"
    local uuid_pat = "^%x%x%x%x%x%x%x%x%-%x%x%x%x%-%x%x%x%x%-%x%x%x%x%-%x%x%x%x%x%x%x%x%x%x%x%x$"

    local function is_uuid(value)
        if type(value) ~= "string" then
            return false
        end
        return string.match(string.lower(value), uuid_pat) ~= nil
    end

    local tenant_id = nil
    local state = record["State"]
    if type(state) == "table" then
        tenant_id = state["tenantId"] or state["TenantId"]
    end
    if tenant_id == nil then
        tenant_id = record["tenantId"] or record["TenantId"]
    end

    if is_uuid(tenant_id) then
        record["org_id"] = tenant_id
    else
        record["org_id"] = ops
    end

    record["service"] = tag

    return 1, timestamp, record
end
