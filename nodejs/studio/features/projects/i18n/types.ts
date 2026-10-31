import type { ProjectRole } from "@/shared/project/types";

/**
 * projects feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type ProjectsFeatureUiText = {
  projects: {
    switcher: {
      label: string;
      option: (displayName: string, roleLabel: string) => string;
    };
    roles: Record<ProjectRole, string>;
    members: {
      title: string;
      description: string;
      forbidden: string;
      emptyProject: string;
      candidateLabel: string;
      candidateRequired: string;
      emptyCandidates: string;
      kinds: {
        user: string;
        serviceAccount: string;
        system: string;
      };
      label: (displayName: string, kindLabel: string) => string;
      roleLabel: string;
      grant: string;
      granting: string;
      change: string;
      revoke: string;
      revoking: string;
      empty: string;
      granted: string;
      revoked: string;
    };
    admin: {
      title: string;
      description: string;
      forbidden: string;
      empty: string;
      definitionClosed: string;
      select: string;
      /** 作成画面（`/projects/new`）へのボタン。 */
      create: string;
    };
    create: {
      title: string;
      description: string;
      noPermission: string;
      slugLabel: string;
      slugHint: string;
      displayNameLabel: string;
      descriptionLabel: string;
      submit: string;
      submitting: string;
      /** 欄ごとの項目エラー。キーは `ProjectCreateFieldErrorKind`。 */
      errors: {
        required: string;
        tooLong: (maxLength: number) => string;
        duplicate: string;
      };
      created: (displayName: string) => string;
      createdReloadRequired: (displayName: string) => string;
    };
  };
};
