import "./globals.css";
import type { Metadata } from "next";
import { cookies } from "next/headers";
import Script from "next/script";
import { ProjectProvider } from "@/shared/project/projectContext";
import { ProjectAwareAppShell } from "@/features/projects/ui/ProjectAwareAppShell";
import { PermissionsProvider } from "@/shared/auth/permissionsContext";
import { fetchIsTenantAdminServer } from "@/shared/auth/serverAuthMe";
import { resolveLocale } from "@/shared/i18n/i18n";
import { DEFAULT_THEME, resolveTheme } from "@/shared/lib/theme";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";
import { resolveSideNavCollapsed, SIDE_NAV_COOKIE_KEY } from "@/shared/ui/navigation/sideNavState";

/**
 * Next.js のルートメタデータ（ファビコン等）。
 * タブのアイコンは紺のタイルに載せたマーク（白と黒のどちらのタブでも読める）。
 * Apple のホーム画面用は SVG を受け付けないため、PNG のまま残す。
 */
export const metadata: Metadata = {
  icons: {
    icon: { url: "/brand/icon-tile.svg", type: "image/svg+xml" },
    apple: "/brand/icon-mark.png",
  },
};

/**
 * アプリ全体のルートレイアウト。テーマ・ロケール・アプリシェル（ヘッダーとサイドメニュー）を子ページへ提供する。
 */
export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const cookieStore = await cookies();
  const locale = resolveLocale(cookieStore.get("ui-lang")?.value);
  const theme = resolveTheme(cookieStore.get("ui-theme")?.value) ?? DEFAULT_THEME;
  const sideNavCollapsed = resolveSideNavCollapsed(cookieStore.get(SIDE_NAV_COOKIE_KEY)?.value);
  const isTenantAdmin = await fetchIsTenantAdminServer();

  return (
    <html lang={locale} data-theme={theme} suppressHydrationWarning>
      <head>
        <Script src="/theme-init.js" strategy="beforeInteractive" />
      </head>
      <body className="min-h-screen bg-md-surface-container-high text-md-on-surface">
        <UiTextProvider locale={locale}>
          <ProjectProvider>
            <PermissionsProvider>
              <ProjectAwareAppShell
                theme={theme}
                locale={locale}
                isTenantAdmin={isTenantAdmin}
                initialCollapsed={sideNavCollapsed}
              >
                {children}
              </ProjectAwareAppShell>
            </PermissionsProvider>
          </ProjectProvider>
        </UiTextProvider>
      </body>
    </html>
  );
}
