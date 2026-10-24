import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement } from "react";
import { ProjectProvider } from "@/features/projects/projectContext";
import type { ProjectSummary } from "@/features/projects/types";
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

type RenderWithUiTextOptions = RenderOptions & {
  locale?: Locale;
  /** 省略時は `defaultTestProjects`。空配列で未所属を表す。 */
  projects?: ProjectSummary[];
};

/**
 * `UiTextProvider` と所属 Project でラップしてコンポーネントを描画する。
 */
export function renderWithUiText(ui: ReactElement, options: RenderWithUiTextOptions = {}) {
  const { locale = "ja", projects = defaultTestProjects, ...renderOptions } = options;
  return render(
    <UiTextProvider locale={locale}>
      <ProjectProvider initialProjects={projects}>{ui}</ProjectProvider>
    </UiTextProvider>,
    renderOptions,
  );
}
