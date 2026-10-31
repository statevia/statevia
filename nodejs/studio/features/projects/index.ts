/**
 * features/projects の公開面。他の feature はここから import する（内部のファイルを直接 import しない）。
 * Project の文脈・ロール判定・所属一覧は shared/project にある。
 */
export { listAdminProjects } from "./api";
