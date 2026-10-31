// @ts-check
/// <reference types="node" />
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import eslint from "@eslint/js";
import { defineConfig } from "eslint/config";
import tseslint from "typescript-eslint";
import react from "eslint-plugin-react";
import reactHooks from "eslint-plugin-react-hooks";
import jsxA11y from "eslint-plugin-jsx-a11y";
import jsdoc from "eslint-plugin-jsdoc";
import sonarjs from "eslint-plugin-sonarjs";
import globals from "globals";

const tsconfigRootDir = path.dirname(fileURLToPath(
    import.meta.url));

const exportJsdocContexts = [
    "ExportNamedDeclaration > FunctionDeclaration",
    "ExportNamedDeclaration > VariableDeclaration",
    "ExportDefaultDeclaration > FunctionDeclaration",
    "ExportNamedDeclaration > TSInterfaceDeclaration",
    "ExportNamedDeclaration > TSTypeAliasDeclaration",
    "ExportNamedDeclaration > ClassDeclaration",
];

/** AST selector fragment: literal value contains hiragana or katakana. */
const japaneseCharClassInLiteral = String.raw `[value=/[\u3040-\u309F\u30A0-\u30FF]/]`;

/**
 * \u30A2\u30D7\u30EA\u306E\u30B3\u30FC\u30C9\uFF08app / features / shared / middleware\uFF09\u3067\u4F7F\u308F\u306A\u3044\u30A8\u30A4\u30EA\u30A2\u30B9\u3002
 * `@/tests/*` \u3068 `@/app/*` \u306F\u30C6\u30B9\u30C8\u304C import \u3059\u308B\u305F\u3081\u306B\u7F6E\u3044\u3066\u3044\u308B\uFF08docs/architecture/ui-studio-structure.md \u00A72\uFF09\u3002
 */
const testOnlyAliasPatterns = [{
    group: ["@/tests", "@/tests/*", "@/app", "@/app/*"],
    message: "@/tests/* \u3068 @/app/* \u306F\u30C6\u30B9\u30C8\u5C02\u7528\u306E\u30A8\u30A4\u30EA\u30A2\u30B9\u3067\u3059\u3002\u30A2\u30D7\u30EA\u306E\u30B3\u30FC\u30C9\u3067\u306F\u4F7F\u308F\u306A\u3044\u3067\u304F\u3060\u3055\u3044\uFF08route \u304B\u3089 feature \u3092\u53C2\u7167\u3059\u308B\uFF09\u3002",
}];

/** feature \u306E\u540D\u524D\uFF08features/ \u76F4\u4E0B\u306E\u30C7\u30A3\u30EC\u30AF\u30C8\u30EA\uFF09\u3002feature \u3092\u8DB3\u3059\u3068\u3001\u5883\u754C\u306E\u30EB\u30FC\u30EB\u3082\u81EA\u52D5\u3067\u8DB3\u3055\u308C\u308B\u3002 */
const featureNames = fs.readdirSync(path.join(tsconfigRootDir, "features"), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name);

/**
 * feature \u5185\u306E\u30D5\u30A1\u30A4\u30EB\u304C\u5B88\u308B import \u306E\u5236\u7D04\uFF08ui-studio-structure.md \u00A73\uFF09\u3002
 * \u4ED6\u306E feature \u306F\u516C\u958B\u9762\uFF08features/<name>/index.ts\uFF09\u3060\u3051\u3092\u4F7F\u3044\u3001\u76F8\u5BFE import \u3067 feature \u306E\u5916\u3078\u51FA\u306A\u3044\u3002
 * @param {string} featureName \u5BFE\u8C61\u306E feature\u3002
 * @param {boolean} topLevel feature \u76F4\u4E0B\u306E\u30D5\u30A1\u30A4\u30EB\u304B\uFF08\u76F4\u4E0B\u3067\u306F `../` \u304C\u3059\u3067\u306B feature \u306E\u5916\u3092\u6307\u3059\uFF09\u3002
 * @returns {import("eslint").Linter.RuleEntry} no-restricted-imports \u306E\u8A2D\u5B9A\u3002
 */
const featureImportRestrictions = (featureName, topLevel) => ["error", {
    patterns: [
        ...testOnlyAliasPatterns,
        {
            regex: `^@/features/(?!${featureName}/)[^/]+/`,
            message: "\u4ED6\u306E feature \u306E\u5185\u90E8\u306F import \u3057\u306A\u3044\u3067\u304F\u3060\u3055\u3044\u3002\u516C\u958B\u9762 @/features/<name>\uFF08features/<name>/index.ts\uFF09\u304B\u3089 import \u3057\u307E\u3059\u3002",
        },
        {
            regex: topLevel ? String.raw `^\.\./` : String.raw `^\.\./\.\./`,
            message: "\u76F8\u5BFE import \u3067 feature \u306E\u5916\u3078\u51FA\u306A\u3044\u3067\u304F\u3060\u3055\u3044\u3002feature \u5185\u306E\u76F8\u5BFE import \u306F 1 \u6BB5\u4E0A\u307E\u3067\u3001\u305D\u308C\u3088\u308A\u9060\u3044\u3082\u306E\u306F @/ \u306E\u30A8\u30A4\u30EA\u30A2\u30B9\u3067\u66F8\u304D\u307E\u3059\u3002",
        },
    ],
}];

export default defineConfig({
    ignores: [
        ".next/**",
        "node_modules/**",
        "coverage/**",
        "**/dist/**",
        "postcss.config.js",
        "vitest.config.ts",
        "eslint.config.js",
        "playwright.config.ts",
        "e2e/**",
    ],
}, {
    files: ["**/*.{ts,tsx}"],
    extends: [
        eslint.configs.recommended,
        ...tseslint.configs.recommended,
        ...tseslint.configs.recommendedTypeChecked,
    ],
    languageOptions: {
        parserOptions: {
            projectService: true,
            tsconfigRootDir,
        },
        globals: {
            ...globals.browser,
            ...globals.node,
        },
    },
    plugins: {
        react,
        // eslint-plugin-react-hooks の flat configs 型と ESLint Plugin 型が不一致のため窄める
        "react-hooks": /** @type {import("eslint").ESLint.Plugin} */ (
            /** @type {unknown} */
            (reactHooks)
        ),
        "jsx-a11y": jsxA11y,
        jsdoc,
    },
    settings: {
        react: {
            version: "detect",
        },
    },
    rules: {
        ...react.configs.flat.recommended.rules,
        "react-hooks/rules-of-hooks": "error",
        "react-hooks/exhaustive-deps": "error",
        ...jsxA11y.configs.recommended.rules,
        "react/react-in-jsx-scope": "off",
        "react/prop-types": "off",
        // Sonar S6819 相当（role="dialog" より <dialog> など）。jsx-a11y の recommended には含まれない
        "jsx-a11y/prefer-tag-over-role": "error",
        "no-nested-ternary": "error",
        "@typescript-eslint/consistent-type-assertions": [
            "error",
            { assertionStyle: "as", objectLiteralTypeAssertions: "never" },
        ],
        "jsdoc/require-jsdoc": [
            "error",
            {
                publicOnly: true,
                require: {
                    FunctionDeclaration: false,
                    MethodDefinition: false,
                    ClassDeclaration: false,
                },
                contexts: exportJsdocContexts,
            },
        ],
        "jsdoc/require-description": "off",
        "jsdoc/require-param-description": "off",
        "jsdoc/require-returns-description": "off",
        "@typescript-eslint/no-unused-vars": [
            "error",
            {
                argsIgnorePattern: "^_",
                varsIgnorePattern: "^_",
                caughtErrorsIgnorePattern: "^_",
            },
        ],
    },
}, {
    // SonarQube Cloud（statevia_statevia_ui）の指摘を編集時・npm run lint で拾う。
    // 範囲は sonar-project.properties の sonar.sources と同じ（tests/ は Sonar のスキャン対象外のため含めない）。
    files: ["app/**/*.{ts,tsx}", "features/**/*.{ts,tsx}", "shared/**/*.{ts,tsx}", "middleware.ts"],
    extends: [sonarjs.configs.recommended],
    rules: {
        // サーバー描画向けの実行時ガード（globalThis.sessionStorage === undefined など）を
        // 型上は常に定義済みとみなして誤検知するため off
        "sonarjs/different-types-comparison": "off",
        // 意図的に複数の型を返す関数（判別共用体の生成・パース結果など）まで指摘するため off
        "sonarjs/function-return-type": "off",
    },
}, {
    files: ["**/i18n/**"],
    rules: {
        // 「パスワード」等のラベル文言を資格情報と誤検知するため off（SonarCloud でも S2068 を同範囲で除外済み）
        "sonarjs/no-hardcoded-passwords": "off",
    },
}, {
    files: ["app/**/*.tsx"],
    rules: {
        "no-restricted-syntax": [
            "error",
            {
                selector: `JSXText${japaneseCharClassInLiteral}`,
                message: "UI 文言は i18n 辞書（useUiText）経由にしてください。",
            },
            {
                selector: `JSXAttribute[name.name='aria-label'] > Literal${japaneseCharClassInLiteral}`,
                message: "aria-label は i18n 辞書経由にしてください。",
            },
            {
                selector: `JSXAttribute[name.name='title'] > Literal${japaneseCharClassInLiteral}`,
                message: "title 属性は i18n 辞書経由にしてください。",
            },
        ],
    },
}, {
    files: ["app/**/*.{ts,tsx}", "features/**/*.{ts,tsx}", "shared/**/*.{ts,tsx}", "middleware.ts"],
    rules: {
        "no-restricted-imports": ["error", { patterns: testOnlyAliasPatterns }],
    },
},
...featureNames.flatMap((featureName) => [{
    files: [`features/${featureName}/**/*.{ts,tsx}`],
    rules: {
        "no-restricted-imports": featureImportRestrictions(featureName, false),
    },
}, {
    files: [`features/${featureName}/*.{ts,tsx}`],
    rules: {
        "no-restricted-imports": featureImportRestrictions(featureName, true),
    },
}]), {
    files: ["shared/i18n/uiText.ts", "shared/i18n/uiText.en.ts"],
    rules: {
        "jsdoc/require-jsdoc": "off",
        // 辞書合成のため shared/i18n のルート辞書のみ feature i18n 切片の import を許可する
        "no-restricted-imports": "off",
    },
}, {
    files: ["shared/**/*.{ts,tsx}"],
    ignores: ["shared/i18n/uiText.ts", "shared/i18n/uiText.en.ts"],
    rules: {
        "no-restricted-imports": [
            "error",
            {
                patterns: [...testOnlyAliasPatterns, {
                        group: ["@/features", "@/features/*"],
                        message: "shared は features を import してはいけません（依存方向: app → features → shared）。辞書合成は shared/i18n/uiText*.ts のみ例外。",
                    },
                    {
                        group: ["**/features/**", "features/**", "*/features/*"],
                        message: "shared は features を import してはいけません（依存方向: app → features → shared）。辞書合成は shared/i18n/uiText*.ts のみ例外。",
                    },
                ],
            },
        ],
    },
}, {
    files: ["tests/**/*.{ts,tsx}"],
    languageOptions: {
        globals: {
            ...globals.browser,
            ...globals.node,
            ...globals.vitest,
        },
    },
    rules: {
        "jsdoc/require-jsdoc": "off",
        "@typescript-eslint/require-await": "off",
        "@typescript-eslint/no-floating-promises": "off",
        "@typescript-eslint/consistent-type-assertions": "off",
        "@typescript-eslint/no-unsafe-assignment": "off",
        "@typescript-eslint/no-unsafe-return": "off",
    },
}, );