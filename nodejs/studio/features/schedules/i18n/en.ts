import type { SchedulesFeatureUiText } from "./types";

/**
 * schedules feature の英語辞書切片。
 */
export const schedulesUiTextEn: SchedulesFeatureUiText = {
  schedules: {
    title: "Schedules",
    description: "Runs a definition at set times. Executions are started with the selected service account.",
    create: "Create schedule",
    empty: "There are no schedules to show. Only schedules for definitions in projects you belong to are shown.",
    joinProjectLink: "Manage project members",
    loadFailed: "Failed to load schedules.",
    retry: "Retry",
    definitionNamesUnavailable: "Definition names could not be loaded, so definition IDs are shown.",
    columns: {
      name: "Name",
      definition: "Definition",
      cron: "Cron",
      timeZone: "Time zone",
      overlap: "Concurrent executions",
      status: "Status",
      nextFireAt: "Next execution",
      actions: "Actions"
    },
    overlap: {
      skip: "Skip if the previous run is still going",
      allow: "Start even if the previous run is still going"
    },
    enabled: "Enabled",
    disabled: "Disabled",
    actions: {
      edit: "Edit",
      delete: "Delete",
      confirmDelete: "Delete",
      deleting: "Deleting…",
      run: "Run now",
      confirmRun: "Run",
      running: "Starting…",
      cancel: "Cancel",
      runConfirmNote:
        "Starts one execution. The next scheduled run does not move. It starts even if the previous run is still going.",
      runStarted: (displayId) => `Started execution ${displayId}.`,
      deleted: (name) => `Deleted ${name}.`
    },
    form: {
      createTitle: "Create schedule",
      editTitle: "Edit schedule",
      backToList: "Back to schedules",
      notFound: "This schedule was not found. It was deleted, or it belongs to a definition in a project you are not a member of.",
      forbidden: "This schedule's definition is not in your definition access.",
      loadFailed: "Failed to load the schedule.",
      nameLabel: "Name",
      nameHint: "ASCII letters or digits. Space, hyphen, underscore, and dot are allowed only between them.",
      definitionLabel: "Definition",
      definitionPlaceholder: "Select a definition",
      definitionsLoadFailed: "Failed to load definitions.",
      definitionsTruncated: (limit) => `There are more than ${limit} definitions, so only some are listed.`,
      currentDefinitionOption: (definitionId) => `Current definition (${definitionId})`,
      cronLabel: "Cron",
      cronHint: "Five fields (minute hour day month weekday). Example: 0 9 * * 1-5",
      timeZoneLabel: "Time zone",
      timeZoneHint: "An IANA time zone name, such as Asia/Tokyo. An empty value is not treated as UTC.",
      overlapLabel: "Concurrent executions",
      runAsLabel: "Execution service account",
      runAsPlaceholder: "Select a service account",
      runAsLoadFailed: "Failed to load service accounts.",
      runAsEmpty: "There are no active service accounts. Create one on the Service accounts page first.",
      currentRunAsOption: (principalId) => `Current service account (${principalId})`,
      versionLabel: "Version (optional)",
      versionHintCreate: "Leave empty to run the latest version when the execution starts.",
      versionHintEdit: "Leave empty to keep the current version setting.",
      inputLabel: "Input (optional, JSON)",
      inputHint: "Input passed when the execution starts. Left out when empty.",
      inputHintEdit: "A saved input cannot be cleared.",
      enabledLabel: "Enabled",
      submitCreate: "Create",
      submitSave: "Save",
      saving: "Saving…",
      saved: "Saved.",
      unchanged: "There are no changes to save.",
      errors: {
        required: "This field is required.",
        nameTooLong: (maxLength) => `Name must be at most ${maxLength} characters.`,
        nameFormat: "Use ASCII letters or digits. Space, hyphen, underscore, and dot are allowed only between them.",
        cronFormat: "Enter cron with five fields.",
        versionFormat: "Enter a version number of 1 or more.",
        versionRequired: "This schedule pins a version. Enter the version of the new definition.",
        inputFormat: "Enter the input as JSON.",
        inputCannotClear: "A saved input cannot be cleared."
      }
    },
    serverErrors: {
      runAsUnusable:
        "This service account cannot run the schedule. It must be an active service account with Executor or higher in the definition's project.",
      membersLink: "Manage project members",
      runAsMissingPermission:
        "This service account does not have permission to start executions. Add the service account to a group that has the permission.",
      serviceAccountsLink: "Manage service accounts",
      definitionUnavailable: "This definition has been deleted, so it cannot run.",
      disableHint: "You can save it if you disable it.",
      runAsGrantDenied: "This definition is not in the execution service account's definition access.",
      projectAccessDenied: "You need Executor or higher in the definition's project.",
      notFound: "The schedule or definition was not found. It was deleted, or it is in a project you are not a member of."
    }
  }
};
