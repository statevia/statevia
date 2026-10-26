import type { ProjectsFeatureUiText } from "./types";

/**
 * projects feature の英語辞書切片。
 */
export const projectsUiTextEn: ProjectsFeatureUiText = {
  projects: {
    switcher: {
      label: "Project",
      option: (displayName, roleLabel) => `${displayName} (${roleLabel})`,
    },
    roles: {
      reader: "Reader",
      executor: "Executor",
      publisher: "Publisher",
      admin: "Admin",
    },
    members: {
      title: "Project members",
      description: "Grant a role to a user or an API key.",
      forbidden: "Project admin role is required to manage members.",
      emptyProject: "You are not a member of any project.",
      candidateLabel: "Add",
      candidateRequired: "Choose a user or an API key to add.",
      emptyCandidates: "There is no user or API key left to add.",
      kinds: {
        user: "User",
        apiKey: "API key",
        system: "System",
      },
      label: (displayName, kindLabel) =>
        displayName.trim().length > 0 ? `${displayName} (${kindLabel})` : kindLabel,
      roleLabel: "Role",
      grant: "Grant",
      granting: "Granting...",
      change: "Change",
      revoke: "Revoke",
      revoking: "Revoking...",
      empty: "No members.",
      granted: "Member updated.",
      revoked: "Member revoked.",
    },
    admin: {
      title: "Tenant projects",
      description: "Manage every project and its members in this tenant. The definition editor stays closed when you are not a member.",
      forbidden: "Tenant administrator permission is required to list projects.",
      empty: "No projects.",
      definitionClosed: "Definition detail and the editor stay closed for projects you do not belong to.",
      select: "Project",
    },
  },
};
