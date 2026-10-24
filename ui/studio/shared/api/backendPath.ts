/**
 * UI の `/api/core` 相対パスを Service API の v1 パスへ写す。
 * @param pathParts 先頭がリソース名のセグメント。
 * @returns `v1` 付きのパス。未知の先頭はそのまま。
 */
export function pathForBackend(pathParts: string[]): string[] {
  if (pathParts[0] === "executions") return ["v1", "executions", ...pathParts.slice(1)];
  if (pathParts[0] === "definitions") return ["v1", "definitions", ...pathParts.slice(1)];
  if (pathParts[0] === "actions") return ["v1", "actions", ...pathParts.slice(1)];
  if (pathParts[0] === "graphs") return ["v1", "graphs", ...pathParts.slice(1)];
  if (pathParts[0] === "admin") return ["v1", "admin", ...pathParts.slice(1)];
  if (pathParts[0] === "projects") return ["v1", "projects", ...pathParts.slice(1)];
  if (pathParts[0] === "auth") return ["v1", "auth", ...pathParts.slice(1)];
  if (pathParts[0] === "events") return ["v1", "events", ...pathParts.slice(1)];
  if (pathParts[0] === "event-subscriptions") return ["v1", "event-subscriptions", ...pathParts.slice(1)];
  return pathParts;
}
