import type { ProjectsFeatureUiText } from "./types";

/**
 * projects feature の日本語辞書切片。
 */
export const projectsUiTextJa: ProjectsFeatureUiText = {
  projects: {
    switcher: {
      label: "プロジェクト",
      members: "メンバー",
      option: (displayName, roleLabel) => `${displayName}（${roleLabel}）`,
    },
    roles: {
      reader: "閲覧",
      executor: "実行",
      publisher: "公開",
      admin: "管理",
    },
    members: {
      title: "プロジェクトメンバー",
      description: "ユーザーと API キーにロールを付与します。",
      forbidden: "メンバー管理にはプロジェクト管理者ロールが必要です。",
      emptyProject: "所属しているプロジェクトがありません。",
      candidateLabel: "追加する相手",
      candidateRequired: "追加するユーザーまたは API キーを選んでください。",
      emptyCandidates: "追加できるユーザーも API キーもありません。",
      kinds: {
        user: "ユーザー",
        apiKey: "API キー",
        system: "システム",
      },
      label: (displayName, kindLabel) =>
        displayName.trim().length > 0 ? `${displayName}（${kindLabel}）` : kindLabel,
      roleLabel: "ロール",
      grant: "付与",
      granting: "付与中...",
      change: "変更",
      revoke: "剥奪",
      revoking: "剥奪中...",
      empty: "メンバーはいません。",
      granted: "メンバーを更新しました。",
      revoked: "メンバーを剥奪しました。",
    },
    admin: {
      title: "テナントのプロジェクト",
      description: "テナント内の全プロジェクトとメンバーを管理します。未所属の定義エディタは開きません。",
      forbidden: "プロジェクト一覧にはテナント管理者権限が必要です。",
      empty: "プロジェクトはありません。",
      definitionClosed: "未所属のプロジェクトでは定義の詳細とエディタを開きません。",
      select: "プロジェクト",
    },
  },
};
