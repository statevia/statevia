import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement } from "react";
import { ProjectProvider } from "@/shared/project/projectContext";
import type { ProjectSummary } from "@/shared/project/types";
import { PermissionsProvider } from "@/shared/auth/permissionsContext";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";
import type { Locale } from "@/shared/i18n/i18n";

/** テスト用の所属 Project。 */
export const testProjectId = "11111111-1111-4111-8111-111111111111";

/** 画面テストの既定所属。admin なので作成・編集・実行の導線が出る。 */
export const defaultTestProjects: ProjectSummary[] = [
  {
    projectId: testProjectId,
    slug: "default",
    displayName: "Default",
    description: null,
    createdAt: "2026-01-01T00:00:00Z",
    role: "admin",
  },
];

/** 画面テストの既定グループ権限。定義と実行の読み書きができる。 */
export const defaultTestPermissions: readonly string[] = [
  "definitions.read",
  "definitions.write",
  "executions.read",
  "executions.write",
];

type RenderWithUiTextOptions = RenderOptions & {
  locale?: Locale;
  /** 省略時は `defaultTestProjects`。空配列で未所属を表す。 */
  projects?: ProjectSummary[];
  /** 省略時は `defaultTestPermissions`。空配列で権限なしを表す。 */
  permissions?: readonly string[];
};

/**
 * `UiTextProvider`、所属 Project、グループ権限でラップしてコンポーネントを描画する。
 */
export function renderWithUiText(ui: ReactElement, options: RenderWithUiTextOptions = {}) {
  const {
    locale = "ja",
    projects = defaultTestProjects,
    permissions = defaultTestPermissions,
    ...renderOptions
  } = options;
  return render(
    <UiTextProvider locale={locale}>
      <ProjectProvider initialProjects={projects}>
        <PermissionsProvider initialPermissions={permissions}>{ui}</PermissionsProvider>
      </ProjectProvider>
    </UiTextProvider>,
    renderOptions,
  );
}
