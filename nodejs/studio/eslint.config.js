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
 * アプリのコード（app / features / shared / middleware）で使わないエイリアス。
 * `@/tests/*` と `@/app/*` はテストが import するために置いている（docs/architecture/ui-studio-structure.md §2）。
 */
const testOnlyAliasPatterns = [{
    group: ["@/tests", "@/tests/*", "@/app", "@/app/*"],
    message: "@/tests/* と @/app/* はテスト専用のエイリアスです。アプリのコードでは使わないでください（route から feature を参照する）。",
}];

/** feature の名前（features/ 直下のディレクトリ）。feature を足すと、境界のルールも自動で足される。 */
const featureNames = fs.readdirSync(path.join(tsconfigRootDir, "features"), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name);

/**
 * feature 内のファイルが守る import の制約（ui-studio-structure.md §3）。
 * 他の feature は公開面（features/<name>/index.ts）だけを使い、相対 import で feature の外へ出ない。
 * @param {string} featureName 対象の feature。
 * @param {boolean} topLevel feature 直下のファイルか（直下では `../` がすでに feature の外を指す）。
 * @returns {import("eslint").Linter.RuleEntry} no-restricted-imports の設定。
 */
const featureImportRestrictions = (featureName, topLevel) => ["error", {
    patterns: [
        ...testOnlyAliasPatterns,
        {
            regex: `^@/features/(?!${featureName}/)[^/]+/`,
            message: "他の feature の内部は import しないでください。公開面 @/features/<name>（features/<name>/index.ts）から import します。",
        },
        {
            regex: topLevel ? String.raw `^\.\./` : String.raw `^\.\./\.\./`,
            message: "相対 import で feature の外へ出ないでください。feature 内の相対 import は 1 段上まで、それより遠いものは @/ のエイリアスで書きます。",
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