import type { TenantNotificationSmtp } from "@/features/admin/notificationSmtp";
import { AdminNotificationSmtpPageClient } from "@/features/admin/ui/AdminNotificationSmtpPageClient";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithUiText } from "../testUtils";

vi.mock("@/shared/api/client", () => ({
  apiGet: vi.fn(),
  apiPut: vi.fn(),
  apiDelete: vi.fn()
}));

import { apiDelete, apiGet, apiPut } from "@/shared/api/client";

const emptySmtp: TenantNotificationSmtp = {
  configured: false,
  host: null,
  port: null,
  user: null,
  defaultFrom: null,
  passwordConfigured: false
};

const storedSmtp: TenantNotificationSmtp = {
  configured: true,
  host: "smtp.example.com",
  port: 587,
  user: "mailer",
  defaultFrom: "notify@example.com",
  passwordConfigured: true
};

describe("AdminNotificationSmtpPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPut).mockReset();
    vi.mocked(apiDelete).mockReset();
    vi.mocked(apiGet).mockResolvedValue(emptySmtp);
  });

  it("未設定ではパスワードが空で、削除を呼ばない", async () => {
    renderWithUiText(<AdminNotificationSmtpPageClient />);

    expect(await screen.findByText("未設定")).toBeInTheDocument();
    expect(screen.getByLabelText("パスワード")).toHaveValue("");
    expect(screen.queryByRole("button", { name: "削除" })).not.toBeInTheDocument();
    expect(apiDelete).not.toHaveBeenCalled();
    expect(apiGet).toHaveBeenCalledWith("/admin/notification-smtp");
  });

  it("設定済みでもパスワード欄は空から始まる", async () => {
    vi.mocked(apiGet).mockResolvedValue(storedSmtp);

    renderWithUiText(<AdminNotificationSmtpPageClient />);

    expect(await screen.findByText("設定済み")).toBeInTheDocument();
    expect(screen.getByLabelText("ホスト")).toHaveValue("smtp.example.com");
    expect(screen.getByLabelText("パスワード")).toHaveValue("");
    expect(screen.getByText("パスワードは保存済みです。この欄は空から始まります。")).toBeInTheDocument();
  });

  it("ユーザーだけ入れて保存しても PUT しない", async () => {
    renderWithUiText(<AdminNotificationSmtpPageClient />);
    await screen.findByText("未設定");

    fireEvent.change(screen.getByLabelText("ホスト"), { target: { value: "smtp.example.com" } });
    fireEvent.change(screen.getByLabelText("ポート"), { target: { value: "587" } });
    fireEvent.change(screen.getByLabelText("ユーザー"), { target: { value: "mailer" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    expect(await screen.findByText("ユーザーとパスワードは、両方入れるか両方空にしてください。")).toBeInTheDocument();
    expect(apiPut).not.toHaveBeenCalled();
  });

  it("保存に成功すると再取得し、パスワードを状態へ戻さない", async () => {
    vi.mocked(apiGet).mockResolvedValueOnce(emptySmtp).mockResolvedValueOnce(storedSmtp);
    vi.mocked(apiPut).mockResolvedValue(storedSmtp);
    renderWithUiText(<AdminNotificationSmtpPageClient />);
    await screen.findByText("未設定");

    fireEvent.change(screen.getByLabelText("ホスト"), { target: { value: "smtp.example.com" } });
    fireEvent.change(screen.getByLabelText("ポート"), { target: { value: "587" } });
    fireEvent.change(screen.getByLabelText("ユーザー"), { target: { value: "mailer" } });
    fireEvent.change(screen.getByLabelText("パスワード"), { target: { value: "secret" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/notification-smtp", {
        host: "smtp.example.com",
        port: 587,
        user: "mailer",
        password: "secret",
        defaultFrom: ""
      });
    });
    expect(await screen.findByText("設定済み")).toBeInTheDocument();
    expect(screen.getByLabelText("パスワード")).toHaveValue("");
    expect(apiGet).toHaveBeenCalledTimes(2);
  });

  it("保存が失敗するとパスワードを消し、設定済みにしない", async () => {
    vi.mocked(apiPut).mockRejectedValue(new Error("VALIDATION_ERROR"));
    renderWithUiText(<AdminNotificationSmtpPageClient />);
    await screen.findByText("未設定");

    fireEvent.change(screen.getByLabelText("ホスト"), { target: { value: "smtp.example.com" } });
    fireEvent.change(screen.getByLabelText("ポート"), { target: { value: "587" } });
    fireEvent.change(screen.getByLabelText("ユーザー"), { target: { value: "mailer" } });
    fireEvent.change(screen.getByLabelText("パスワード"), { target: { value: "secret" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    expect(await screen.findByText("VALIDATION_ERROR")).toBeInTheDocument();
    expect(screen.getByLabelText("パスワード")).toHaveValue("");
    expect(screen.getByText("未設定")).toBeInTheDocument();
    expect(apiGet).toHaveBeenCalledTimes(1);
  });

  it("削除確認のあと DELETE し、未設定表示へ戻す", async () => {
    vi.mocked(apiGet).mockResolvedValueOnce(storedSmtp).mockResolvedValueOnce(emptySmtp);
    vi.mocked(apiDelete).mockResolvedValue(undefined);
    renderWithUiText(<AdminNotificationSmtpPageClient />);
    await screen.findByText("設定済み");

    fireEvent.click(screen.getByRole("button", { name: "削除" }));
    expect(apiDelete).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "削除する" }));

    await waitFor(() => {
      expect(apiDelete).toHaveBeenCalledWith("/admin/notification-smtp");
    });
    expect(await screen.findByText("未設定")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "削除" })).not.toBeInTheDocument();
  });

  it("削除確認をキャンセルすると DELETE しない", async () => {
    vi.mocked(apiGet).mockResolvedValue(storedSmtp);
    renderWithUiText(<AdminNotificationSmtpPageClient />);
    await screen.findByText("設定済み");

    fireEvent.click(screen.getByRole("button", { name: "削除" }));
    fireEvent.click(screen.getByRole("button", { name: "キャンセル" }));

    expect(apiDelete).not.toHaveBeenCalled();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
