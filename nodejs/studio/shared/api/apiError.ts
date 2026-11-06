/**
 * Service API / BFF のエラー JSON 形状（クライアント側）。
 */
export type ApiError = {
  status?: number;
  error: {
    code: string;
    message: string;
    /**
     * 422 などの補足。Service API はドメイン検証で `{ field }`、形式検証で `[{ field, message }]` を返す。
     */
    details?: Record<string, unknown> | ReadonlyArray<unknown>;
  };
};
