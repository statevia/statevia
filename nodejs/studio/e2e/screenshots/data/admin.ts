import type { PrincipalResourceGrants } from "@/features/admin/resourceGrants";
import type {
  AdminApiKeyListItem,
  AdminGroupDetail,
  AdminGroupListItem,
  AdminServiceAccount,
  AdminUserListItem,
  PermissionDefinitionDto
} from "@/features/admin/types";
import { PROJECT_IDS, screenshotAdmin } from "./common";
import { screenshotDefinitions } from "./definitions";

/** 架空のグループの ID。 */
export const GROUP_IDS = {
  operators: "00000000-0000-4000-8000-000000000301",
  publishers: "00000000-0000-4000-8000-000000000302",
  auditors: "00000000-0000-4000-8000-000000000303",
  batch: "00000000-0000-4000-8000-000000000304"
} as const;

/** 操作できる定義の画面で開くユーザーの ID（鈴木 一郎）。 */
export const RESOURCE_GRANT_USER_ID = "00000000-0000-4000-8000-000000000102";

/** `GET /v1/admin/users` の応答。名前とメールはすべて架空（`example.com`）。 */
export const screenshotUsers: AdminUserListItem[] = [
  {
    userId: "00000000-0000-4000-8000-000000000101",
    principalId: screenshotAdmin.principalId,
    username: "sato.hanako",
    email: "sato.hanako@example.com",
    displayName: "佐藤 花子",
    isTenantAdmin: true,
    isActive: true,
    groupIds: [],
    createdAt: "2026-09-01T00:00:00Z"
  },
  {
    userId: RESOURCE_GRANT_USER_ID,
    principalId: "00000000-0000-4000-8000-000000000112",
    username: "suzuki.ichiro",
    email: "suzuki.ichiro@example.com",
    displayName: "鈴木 一郎",
    isTenantAdmin: false,
    isActive: true,
    groupIds: [GROUP_IDS.operators],
    createdAt: "2026-09-03T01:00:00Z"
  },
  {
    userId: "00000000-0000-4000-8000-000000000103",
    principalId: "00000000-0000-4000-8000-000000000113",
    username: "takahashi.misaki",
    email: "takahashi.misaki@example.com",
    displayName: "高橋 美咲",
    isTenantAdmin: false,
    isActive: true,
    groupIds: [GROUP_IDS.publishers, GROUP_IDS.operators],
    createdAt: "2026-09-10T02:30:00Z"
  },
  {
    userId: "00000000-0000-4000-8000-000000000104",
    principalId: "00000000-0000-4000-8000-000000000114",
    username: "tanaka.ken",
    email: null,
    displayName: "田中 健",
    isTenantAdmin: false,
    isActive: false,
    groupIds: [GROUP_IDS.auditors],
    createdAt: "2026-10-01T04:00:00Z"
  }
];

/** `GET /v1/admin/groups` の応答。 */
export const screenshotGroups: AdminGroupListItem[] = [
  { groupId: GROUP_IDS.operators, name: "operators", isSystem: false, memberCount: 2, permissionCount: 3, updatedAt: "2026-10-05T00:00:00Z" },
  { groupId: GROUP_IDS.publishers, name: "publishers", isSystem: false, memberCount: 1, permissionCount: 4, updatedAt: "2026-10-05T00:00:00Z" },
  { groupId: GROUP_IDS.auditors, name: "auditors", isSystem: false, memberCount: 1, permissionCount: 2, updatedAt: "2026-10-12T00:00:00Z" },
  { groupId: GROUP_IDS.batch, name: "batch-jobs", isSystem: false, memberCount: 0, permissionCount: 2, updatedAt: "2026-10-20T00:00:00Z" }
];

/** `GET /v1/admin/groups/{groupId}` の応答（グループの編集で開く operators）。 */
export const screenshotOperatorsGroup: AdminGroupDetail = {
  groupId: GROUP_IDS.operators,
  name: "operators",
  isSystem: false,
  memberUserIds: [RESOURCE_GRANT_USER_ID, "00000000-0000-4000-8000-000000000103"],
  permissionKeys: ["definitions.read", "executions.read", "executions.write"]
};

/** `GET /v1/admin/permissions` の応答。ラベル・i18n キー・並びは実 API の値（2026-11-19 に取得）。 */
export const screenshotPermissions: PermissionDefinitionDto[] = [
  { permissionKey: "definitions.read", displayLabel: "Read definitions", displayKey: "permissions.definitionsRead", isSystem: true, isDeprecated: false },
  { permissionKey: "definitions.write", displayLabel: "Write definitions", displayKey: "permissions.definitionsWrite", isSystem: true, isDeprecated: false },
  { permissionKey: "executions.read", displayLabel: "Read executions", displayKey: "permissions.executionsRead", isSystem: true, isDeprecated: false },
  { permissionKey: "executions.write", displayLabel: "Write executions", displayKey: "permissions.executionsWrite", isSystem: true, isDeprecated: false },
  { permissionKey: "modules.read", displayLabel: "Read action modules", displayKey: "permissions.modulesRead", isSystem: true, isDeprecated: false },
  { permissionKey: "modules.reload", displayLabel: "Reload action modules", displayKey: "permissions.modulesReload", isSystem: true, isDeprecated: false },
  { permissionKey: "projects.create", displayLabel: "Create projects", displayKey: "permissions.projectsCreate", isSystem: true, isDeprecated: false },
  { permissionKey: "tenant.admin", displayLabel: "Tenant administration", displayKey: "permissions.tenantAdmin", isSystem: true, isDeprecated: false }
];

/**
 * `GET /v1/admin/api-keys` の応答。
 * @remarks 一覧はキーの値を返さず、先頭（`keyPrefix`）だけを返す。先頭も架空の値（実 API と同じ `stv` で始まる 8 文字）。
 */
export const screenshotApiKeys: AdminApiKeyListItem[] = [
  {
    apiKeyId: "00000000-0000-4000-8000-000000000501",
    name: "order-intake",
    keyPrefix: "stv7f2qa",
    allowedScopes: ["definitions.read", "executions.read", "executions.write"],
    expiresAt: "2027-03-31T15:00:00Z",
    lastUsedAt: "2026-11-12T01:10:00Z",
    createdAt: "2026-10-01T00:00:00Z",
    isActive: true
  },
  {
    apiKeyId: "00000000-0000-4000-8000-000000000502",
    name: "reporting",
    keyPrefix: "stv9c4md",
    allowedScopes: ["executions.read"],
    expiresAt: null,
    lastUsedAt: "2026-11-11T22:00:00Z",
    createdAt: "2026-10-15T00:00:00Z",
    isActive: true
  },
  {
    apiKeyId: "00000000-0000-4000-8000-000000000503",
    name: "legacy-import",
    keyPrefix: "stv2h8wz",
    allowedScopes: ["definitions.read", "definitions.write"],
    expiresAt: "2026-10-31T15:00:00Z",
    lastUsedAt: null,
    createdAt: "2026-08-20T00:00:00Z",
    isActive: false
  }
];

/** `GET /v1/admin/service-accounts` の応答（定期実行の実行するサービスアカウントの候補にも使う）。 */
export const screenshotServiceAccounts: AdminServiceAccount[] = [
  {
    serviceAccountId: "00000000-0000-4000-8000-000000000401",
    principalId: "00000000-0000-4000-8000-000000000411",
    name: "nightly-batch",
    isActive: true,
    hasApiKey: false,
    groupIds: [GROUP_IDS.batch],
    createdAt: "2026-10-20T00:00:00Z"
  },
  {
    serviceAccountId: "00000000-0000-4000-8000-000000000402",
    principalId: "00000000-0000-4000-8000-000000000412",
    name: "order-intake",
    isActive: true,
    hasApiKey: true,
    groupIds: [GROUP_IDS.operators],
    createdAt: "2026-10-01T00:00:00Z"
  },
  {
    serviceAccountId: "00000000-0000-4000-8000-000000000403",
    principalId: "00000000-0000-4000-8000-000000000413",
    name: "legacy-import",
    isActive: false,
    hasApiKey: false,
    groupIds: [GROUP_IDS.publishers],
    createdAt: "2026-08-20T00:00:00Z"
  }
];

/** `GET /v1/admin/users/{userId}/resource-grants` の応答（受注処理の Project と、在庫移動の承認の定義を許す）。 */
export const screenshotUserResourceGrants: PrincipalResourceGrants = {
  projectIds: [PROJECT_IDS.orders],
  definitionIds: [screenshotDefinitions[1].resourceId]
};
