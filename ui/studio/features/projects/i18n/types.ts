import type { ProjectRole } from "../types";

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
        apiKey: string;
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
    };
  };
};
