import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPost: vi.fn()
  };
});

import { apiGet, apiPost } from "@/shared/api";
import { listEventSubscriptions, publishTenantEvent } from "@/features/executions/api";

describe("tenant event ingress client", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPost).mockReset();
  });

  it("候補 GET は /event-subscriptions を呼ぶ", async () => {
    vi.mocked(apiGet).mockResolvedValue({ subscriptions: [] });

    await listEventSubscriptions();

    expect(apiGet).toHaveBeenCalledWith("/event-subscriptions");
  });

  it("発行は /events に topic と key だけを送り、resume パスは呼ばない", async () => {
    vi.mocked(apiPost).mockResolvedValue(null);

    await publishTenantEvent({ topic: " orders.updated ", key: " sku " });

    expect(apiPost).toHaveBeenCalledTimes(1);
    expect(apiPost).toHaveBeenCalledWith("/events", { topic: "orders.updated", key: "sku" });
    const [path, body] = vi.mocked(apiPost).mock.calls[0] ?? [];
    expect(String(path)).not.toContain("resume");
    expect(body).not.toHaveProperty("payload");
  });

  it("空の key は送らない", async () => {
    vi.mocked(apiPost).mockResolvedValue(null);

    await publishTenantEvent({ topic: "orders.updated", key: "  " });

    expect(apiPost).toHaveBeenCalledWith("/events", { topic: "orders.updated" });
  });
});
