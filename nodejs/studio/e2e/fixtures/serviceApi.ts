import { randomUUID } from "node:crypto";
import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { serviceApiBase, serviceApiCredentials } from "../support/env";

/** ログイン済みの Service API の呼び出し口。 */
export type ServiceApiSession = {
  /** `Authorization` と `X-Tenant-Id` を付けたヘッダー。 */
  headers: Record<string, string>;
  /** 定義を作る Project（slug が `default`）の ID。 */
  projectId: string;
};

/**
 * Service API へログインし、既定の Project を引く。
 * @param request Playwright の API リクエスト。
 * @returns ログイン済みのヘッダーと Project の ID。
 */
export async function loginServiceApi(request: APIRequestContext): Promise<ServiceApiSession> {
  const login = await request.post(`${serviceApiBase}/v1/auth/login`, { data: serviceApiCredentials });
  expect(login.ok(), await login.text()).toBeTruthy();
  const { accessToken } = (await login.json()) as { accessToken: string };
  const headers = {
    Authorization: `Bearer ${accessToken}`,
    "X-Tenant-Id": serviceApiCredentials.tenantKey
  };

  const projects = await request.get(`${serviceApiBase}/v1/projects`, { headers });
  expect(projects.ok(), await projects.text()).toBeTruthy();
  const projectsJson = (await projects.json()) as
    | { items: { projectId: string; slug: string }[] }
    | { projectId: string; slug: string }[];
  const items = Array.isArray(projectsJson) ? projectsJson : projectsJson.items;
  const defaultProject = items.find((project) => project.slug === "default");
  expect(defaultProject, "slug が default の Project が無い").toBeDefined();

  return { headers, projectId: defaultProject?.projectId ?? "" };
}

/**
 * 定義を作る。
 * @param request Playwright の API リクエスト。
 * @param session ログイン済みの呼び出し口。
 * @param name 定義名（ASCII 識別子）。
 * @param yaml 定義の YAML。
 * @returns 作った定義の表示用 ID。
 */
export async function createDefinition(
  request: APIRequestContext,
  session: ServiceApiSession,
  name: string,
  yaml: string
): Promise<string> {
  const res = await request.post(`${serviceApiBase}/v1/definitions`, {
    headers: { ...session.headers, "X-Idempotency-Key": randomUUID() },
    data: { name, yaml, projectId: session.projectId }
  });
  expect(res.status(), await res.text()).toBe(201);
  return ((await res.json()) as { displayId: string }).displayId;
}

/**
 * 定義を作り、その定義で実行を始める。
 * @param request Playwright の API リクエスト。
 * @param session ログイン済みの呼び出し口。
 * @param name 定義名（ASCII 識別子）。
 * @param yaml 定義の YAML。
 * @returns 始めた実行の表示用 ID。
 */
export async function startExecutionFromYaml(
  request: APIRequestContext,
  session: ServiceApiSession,
  name: string,
  yaml: string
): Promise<string> {
  const definitionId = await createDefinition(request, session, name, yaml);
  const start = await request.post(`${serviceApiBase}/v1/executions`, {
    headers: { ...session.headers, "X-Idempotency-Key": randomUUID() },
    data: { definitionId, input: {} }
  });
  expect(start.status(), await start.text()).toBe(201);
  return ((await start.json()) as { displayId: string }).displayId;
}

/**
 * 実行の状態が期待の値になるまで待つ。
 * @param request Playwright の API リクエスト。
 * @param session ログイン済みの呼び出し口。
 * @param displayId 実行の表示用 ID。
 * @param status 期待する状態（例: `CANCELED`）。
 * @param timeoutMs 待つ上限（ミリ秒）。
 */
export async function waitForExecutionStatus(
  request: APIRequestContext,
  session: ServiceApiSession,
  displayId: string,
  status: string,
  timeoutMs = 30_000
): Promise<void> {
  await expect
    .poll(
      async () => {
        const res = await request.get(`${serviceApiBase}/v1/executions/${encodeURIComponent(displayId)}`, {
          headers: session.headers
        });
        return res.ok() ? ((await res.json()) as { status?: string }).status : `HTTP ${res.status()}`;
      },
      { timeout: timeoutMs, intervals: [200, 500, 1000] }
    )
    .toBe(status);
}

/**
 * 実行のグラフで、待機中（未完了）の Wait ノードの状態名がそろうまで待つ。
 * @param request Playwright の API リクエスト。
 * @param session ログイン済みの呼び出し口。
 * @param displayId 実行の表示用 ID。
 * @param nodeNames 期待する待機中の状態名（並べ替えて比べる）。
 * @param timeoutMs 待つ上限（ミリ秒）。
 */
export async function waitForWaitingNodes(
  request: APIRequestContext,
  session: ServiceApiSession,
  displayId: string,
  nodeNames: string[],
  timeoutMs = 30_000
): Promise<void> {
  await expect
    .poll(
      async () => {
        const res = await request.get(`${serviceApiBase}/v1/executions/${encodeURIComponent(displayId)}/graph`, {
          headers: session.headers
        });
        if (!res.ok()) return [`HTTP ${res.status()}`];
        const graph = (await res.json()) as { nodes: { nodeName?: string; nodeType?: string; completedAt?: string | null }[] };
        return graph.nodes
          .filter((node) => node.nodeType === "Wait" && node.completedAt == null)
          .map((node) => node.nodeName ?? "")
          .sort();
      },
      { timeout: timeoutMs, intervals: [200, 500, 1000] }
    )
    .toEqual([...nodeNames].sort());
}

/**
 * ブラウザで Studio にログインする（Next の `/api/auth/login` が httpOnly Cookie を置く）。
 * @remarks `page.request` はブラウザのコンテキストと Cookie を共有するので、ログイン画面を操作せずに済む。
 * @param page 対象のページ。
 */
export async function loginStudio(page: Page): Promise<void> {
  const res = await page.request.post("/api/auth/login", { data: serviceApiCredentials });
  expect(res.ok(), await res.text()).toBeTruthy();
}

/**
 * E2E で作る定義の名前（衝突しないよう時刻と乱数を付ける）。
 * @param prefix 名前の頭（ASCII 識別子）。
 * @returns 定義名。
 */
export function uniqueDefinitionName(prefix: string): string {
  return `${prefix}_${Date.now()}_${randomUUID().slice(0, 8)}`;
}
