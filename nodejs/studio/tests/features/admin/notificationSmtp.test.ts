import { describe, expect, it } from "vitest";
import {
  buildNotificationSmtpPutBody,
  notificationSmtpFormFromResponse,
  type TenantNotificationSmtp
} from "@/features/admin/notificationSmtp";

const configured: TenantNotificationSmtp = {
  configured: true,
  host: "smtp.example.com",
  port: 587,
  user: "mailer",
  defaultFrom: "notify@example.com",
  passwordConfigured: true
};

describe("notification smtp put body", () => {
  it("ユーザーがありパスワードが空なら送らない", () => {
    const result = buildNotificationSmtpPutBody({
      host: "smtp.example.com",
      port: "587",
      user: "mailer",
      password: "",
      defaultFrom: ""
    });

    expect(result).toEqual({ ok: false, field: "passwordPair" });
  });

  it("ユーザーが空でパスワードだけなら送らない", () => {
    const result = buildNotificationSmtpPutBody({
      host: "smtp.example.com",
      port: "587",
      user: "",
      password: "secret",
      defaultFrom: ""
    });

    expect(result).toEqual({ ok: false, field: "passwordPair" });
  });

  it("ユーザーとパスワードが空の組はパスワードキーを残して送る", () => {
    const result = buildNotificationSmtpPutBody({
      host: " smtp.example.com ",
      port: "25",
      user: "  ",
      password: "",
      defaultFrom: "  "
    });

    expect(result).toEqual({
      ok: true,
      body: {
        host: "smtp.example.com",
        port: 25,
        user: "",
        password: "",
        defaultFrom: ""
      }
    });
  });

  it("パスワードの前後空白は残して送る", () => {
    const result = buildNotificationSmtpPutBody({
      host: "smtp.example.com",
      port: "587",
      user: "mailer",
      password: " secret ",
      defaultFrom: "notify@example.com"
    });

    expect(result).toEqual({
      ok: true,
      body: {
        host: "smtp.example.com",
        port: 587,
        user: "mailer",
        password: " secret ",
        defaultFrom: "notify@example.com"
      }
    });
  });

  it("ポートが範囲外なら送らない", () => {
    expect(
      buildNotificationSmtpPutBody({
        host: "smtp.example.com",
        port: "65536",
        user: "",
        password: "",
        defaultFrom: ""
      })
    ).toEqual({ ok: false, field: "port" });
    expect(
      buildNotificationSmtpPutBody({
        host: "smtp.example.com",
        port: "0",
        user: "",
        password: "",
        defaultFrom: ""
      })
    ).toEqual({ ok: false, field: "port" });
  });
});

describe("notification smtp form", () => {
  it("参照から起こしたフォームのパスワードは空である", () => {
    expect(notificationSmtpFormFromResponse(configured).password).toBe("");
    expect(notificationSmtpFormFromResponse(configured)).not.toHaveProperty("passwordConfigured");
  });
});
