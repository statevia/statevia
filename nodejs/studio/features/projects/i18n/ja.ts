import type { ProjectsFeatureUiText } from "./types";

/**
 * projects feature の日本語辞書切片。
 */
export const projectsUiTextJa: ProjectsFeatureUiText = {
  projects: {
    switcher: {
      label: "プロジェクト",
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
      description: "ユーザーと ServiceAccount にロールを付与します。",
      forbidden: "メンバー管理にはプロジェクト管理者ロールが必要です。",
      emptyProject: "所属しているプロジェクトがありません。",
      candidateLabel: "追加する相手",
      candidateRequired: "追加するユーザーまたは ServiceAccount を選んでください。",
      emptyCandidates: "追加できるユーザーも ServiceAccount もありません。",
      kinds: {
        user: "ユーザー",
        serviceAccount: "ServiceAccount",
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
      create: "プロジェクトを作成",
    },
    create: {
      title: "プロジェクトを作成",
      description: "作成したプロジェクトでは、あなたが管理ロールになります。プロジェクトは削除できません。",
      noPermission: "プロジェクトの作成には projects.create 権限が必要です。",
      slugLabel: "プロジェクトキー",
      slugHint: "テナント内で重複しない名前です。API や CLI からこのプロジェクトを指すときに使います。画面には表示名が出ます。",
      displayNameLabel: "表示名",
      descriptionLabel: "説明（任意）",
      submit: "作成",
      submitting: "作成中...",
      errors: {
        required: "入力してください。",
        tooLong: (maxLength) => `${maxLength} 文字以内で入力してください。`,
        duplicate: "このプロジェクトキーは既に使われています。",
      },
      created: (displayName) => `プロジェクト「${displayName}」を作成し、選択しました。`,
      createdReloadRequired: (displayName) =>
        `プロジェクト「${displayName}」を作成しました。一覧の取得に失敗したため、ページを再読み込みしてください。`,
    },
  },
};
