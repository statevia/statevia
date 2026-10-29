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
      description: "Grant a role to a user or a service account.",
      forbidden: "Project admin role is required to manage members.",
      emptyProject: "You are not a member of any project.",
      candidateLabel: "Add",
      candidateRequired: "Choose a user or a service account to add.",
      emptyCandidates: "There is no user or service account left to add.",
      kinds: {
        user: "User",
        serviceAccount: "Service account",
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
      create: "Create project",
    },
    create: {
      title: "Create project",
      description: "You become the admin of the project you create. Projects cannot be deleted.",
      noPermission: "Creating a project requires the projects.create permission.",
      slugLabel: "Project key",
      slugHint: "A name unique within the tenant. Use it to refer to this project from the API or CLI. Screens show the display name.",
      displayNameLabel: "Display name",
      descriptionLabel: "Description (optional)",
      submit: "Create",
      submitting: "Creating...",
      errors: {
        required: "This field is required.",
        tooLong: (maxLength) => `Use ${maxLength} characters or fewer.`,
        duplicate: "This project key is already in use.",
      },
      created: (displayName) => `Created and selected project "${displayName}".`,
      createdReloadRequired: (displayName) =>
        `Created project "${displayName}". Reload the page because the project list could not be refreshed.`,
    },
  },
};
