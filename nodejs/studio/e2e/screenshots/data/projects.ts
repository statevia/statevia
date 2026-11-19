import type { ProjectGrantCandidate, ProjectMember } from "@/features/projects/types";
import { screenshotServiceAccounts, screenshotUsers } from "./admin";

/** `GET /v1/projects/{projectId}/members` の応答（受注処理の Project のメンバー）。 */
export const screenshotProjectMembers: ProjectMember[] = [
  { principalId: screenshotUsers[0].principalId, displayName: screenshotUsers[0].displayName, kind: "user", role: "admin", createdAt: "2026-09-01T00:00:00Z" },
  { principalId: screenshotUsers[2].principalId, displayName: screenshotUsers[2].displayName, kind: "user", role: "publisher", createdAt: "2026-09-10T03:00:00Z" },
  { principalId: screenshotUsers[1].principalId, displayName: screenshotUsers[1].displayName, kind: "user", role: "executor", createdAt: "2026-09-12T00:00:00Z" },
  {
    principalId: screenshotServiceAccounts[1].principalId,
    displayName: screenshotServiceAccounts[1].name,
    kind: "serviceAccount",
    role: "executor",
    createdAt: "2026-10-01T00:10:00Z"
  },
  {
    principalId: screenshotServiceAccounts[0].principalId,
    displayName: screenshotServiceAccounts[0].name,
    kind: "serviceAccount",
    role: "reader",
    createdAt: "2026-10-20T00:10:00Z"
  }
];

/** `GET /v1/projects/{projectId}/member-candidates` の応答（まだメンバーでないユーザーとサービスアカウント）。 */
export const screenshotMemberCandidates: ProjectGrantCandidate[] = [
  { principalId: screenshotUsers[3].principalId, displayName: screenshotUsers[3].displayName, kind: "user" },
  { principalId: screenshotServiceAccounts[2].principalId, displayName: screenshotServiceAccounts[2].name, kind: "serviceAccount" }
];
