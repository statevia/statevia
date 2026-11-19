import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import type { DefinitionSchemaResponse } from "@/features/definition-editor/types";
import type { DefinitionDTO, PagedDefinitions } from "@/features/definitions/types";
import { PROJECT_IDS } from "./common";

/**
 * 主役の定義（受注の並列処理）の表示用 ID。
 * @remarks 定義の編集と実行グラフの両方で使う。中身は `docs/samples/ui-customer-order-parallel.yaml`。
 */
export const ORDER_DEFINITION_ID = "Rk4mQ8vTn2";

const orderDefinitionYaml = readFileSync(
  fileURLToPath(new URL("../../../../../docs/samples/ui-customer-order-parallel.yaml", import.meta.url)),
  "utf-8"
);

/** 一覧に並べる定義（名前・表示用 ID・最後の実行）。登録日時の新しい順。 */
const definitionRows = [
  {
    displayId: ORDER_DEFINITION_ID,
    resourceId: "00000000-0000-4000-8000-000000000701",
    name: "customer-order-parallel",
    createdAt: "2026-11-10T02:00:00Z",
    latestVersion: 3,
    lastExecution: { displayId: "Wd3pL7sKe9", status: "ACTIVE", startedAt: "2026-11-12T01:12:00Z", updatedAt: "2026-11-12T01:12:02Z", waiting: true }
  },
  {
    displayId: "Hb6tN1cYw5",
    resourceId: "00000000-0000-4000-8000-000000000702",
    name: "stock-transfer-approval",
    createdAt: "2026-11-05T05:30:00Z",
    latestVersion: 2,
    lastExecution: { displayId: "Pz8rF2hJm4", status: "COMPLETED", startedAt: "2026-11-12T00:40:00Z", updatedAt: "2026-11-12T00:41:10Z", waiting: false }
  },
  {
    displayId: "Jc2vX9qLs7",
    resourceId: "00000000-0000-4000-8000-000000000703",
    name: "invoice-monthly-close",
    createdAt: "2026-10-28T08:00:00Z",
    latestVersion: 5,
    lastExecution: { displayId: "Tg5kB3nVd8", status: "FAILED", startedAt: "2026-11-11T15:00:00Z", updatedAt: "2026-11-11T15:02:30Z", waiting: false }
  },
  {
    displayId: "Mf7yA4wRp1",
    resourceId: "00000000-0000-4000-8000-000000000704",
    name: "shipment-delay-alert",
    createdAt: "2026-10-20T01:15:00Z",
    latestVersion: 1,
    lastExecution: { displayId: "Ys1cH6mQz3", status: "COMPLETED", startedAt: "2026-11-12T01:00:00Z", updatedAt: "2026-11-12T01:00:04Z", waiting: false }
  },
  {
    displayId: "Qn9eD5tGk6",
    resourceId: "00000000-0000-4000-8000-000000000705",
    name: "return-request-review",
    createdAt: "2026-10-02T04:45:00Z",
    latestVersion: 4,
    lastExecution: null
  }
] as const satisfies readonly (Omit<DefinitionDTO, "projectId" | "updatedAt" | "yaml" | "deletedAt"> & { name: string })[];

/** 一覧の定義（`yaml` は一覧の応答と同じく null 相当で省く）。 */
export const screenshotDefinitions: DefinitionDTO[] = definitionRows.map((row) => ({
  ...row,
  projectId: PROJECT_IDS.orders,
  updatedAt: row.createdAt,
  deletedAt: null
}));

/**
 * `GET /v1/definitions` の応答（定義一覧と、定期実行の定義の候補）。
 * @param limit 要求の limit。定期実行の画面は 500 で候補をまとめて読む。
 * @returns 1 ページ目。
 */
export function screenshotDefinitionsPage(limit: number): PagedDefinitions {
  return { items: screenshotDefinitions, totalCount: screenshotDefinitions.length, offset: 0, limit, hasMore: false };
}

/** `GET /v1/definitions/{id}` の応答（受注の並列処理。YAML は公式サンプル）。 */
export const screenshotOrderDefinition: DefinitionDTO = {
  ...screenshotDefinitions[0],
  lastExecution: undefined,
  yaml: orderDefinitionYaml
};

/**
 * `GET /v1/definitions/schema/nodes` の応答。
 * @remarks 画面は補完のキーワードにだけ使い、空のときは組み込みのキーワードへ倒す。撮影に写らないので中身は空にする。
 */
export const screenshotNodesSchema: DefinitionSchemaResponse = {
  schemaVersion: "1.0.2",
  nodesVersion: 1,
  schema: {}
};
