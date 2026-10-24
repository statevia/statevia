/**
 * API 失敗が 404 か。
 * @param error `apiGet` などが投げた値。
 * @returns status が 404 のとき true。
 */
export function isApiNotFound(error: unknown): boolean {
  return typeof error === "object"
    && error !== null
    && "status" in error
    && (error as { status?: unknown }).status === 404;
}
