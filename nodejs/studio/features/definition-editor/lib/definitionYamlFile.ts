import { DEFINITION_YAML_MAX_BYTES } from "@/shared/lib/validation/formRules";
import { getUtf8ByteLength } from "@/shared/lib/validation/primitives";

/** 定義 YAML として受け付ける拡張子。大文字小文字は区別しない。 */
const YAML_FILE_EXTENSIONS = [".yaml", ".yml"] as const;

/** ダウンロード名に使えない文字。パス区切りと Windows 予約。 */
const UNSAFE_DOWNLOAD_NAME_CHARACTERS = /[\\/:*?"<>|]/g;

/** 空白またはドット。先頭末尾の除去にだけ使う。 */
const SPACE_OR_DOT = /[\s.]/u;

/** 定義名が空のときのダウンロード名。 */
const DEFAULT_DOWNLOAD_FILE_NAME = "definition.yaml";

/** 読み込み失敗の理由。エディタは変えない。 */
export type DefinitionYamlReadFailureReason = "extension" | "tooLarge" | "readFailed";

/** ローカル YAML の読み込み結果。 */
export type DefinitionYamlReadResult =
  | { ok: true; text: string }
  | { ok: false; reason: DefinitionYamlReadFailureReason };

/**
 * ファイルを UTF-8 テキストとして読む。`File.text()` と同じ結果。
 * @param file 選択ファイル。
 * @returns 全文。
 */
function readFileAsUtf8(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result !== "string") {
        reject(new Error("readFailed"));
        return;
      }
      resolve(reader.result);
    };
    reader.onerror = () => {
      reject(reader.error ?? new Error("readFailed"));
    };
    reader.readAsText(file, "UTF-8");
  });
}

/**
 * ファイル名が `.yaml` / `.yml` か。
 * @param fileName 選択ファイルの名前。
 * @returns 許可拡張子なら true。
 */
export function isAllowedDefinitionYamlFileName(fileName: string): boolean {
  const normalized = fileName.trim().toLowerCase();
  return YAML_FILE_EXTENSIONS.some((extension) => normalized.endsWith(extension));
}

/**
 * 先頭の UTF-8 BOM（U+FEFF）を 1 つ除く。
 * @param text 読み取った全文。
 * @returns BOM を除いたテキスト。
 */
export function stripUtf8Bom(text: string): string {
  return text.startsWith("\uFEFF") ? text.slice(1) : text;
}

/**
 * 保存時と同じ 256KB 上限で判定する。ちょうど上限は許可する。
 * @param text BOM 除去後の YAML。
 * @returns 上限以内なら `ok`。
 */
export function assertDefinitionYamlSize(text: string): "ok" | "tooLarge" {
  return getUtf8ByteLength(text) > DEFINITION_YAML_MAX_BYTES ? "tooLarge" : "ok";
}

/**
 * 先頭と末尾の空白・ドットを除く。中間はそのまま残す。
 * 交互の正規表現は末尾不一致で二次のバックトラックになるため、端の 1 文字判定にする。
 * @param value 使えない文字を置換したあとの定義名。
 * @returns トリム後。全体が対象文字なら空文字。
 */
function trimLeadingAndTrailingSpacesAndDots(value: string): string {
  let start = 0;
  let end = value.length;
  while (start < end && SPACE_OR_DOT.test(value.charAt(start))) {
    start += 1;
  }
  while (end > start && SPACE_OR_DOT.test(value.charAt(end - 1))) {
    end -= 1;
  }
  return value.slice(start, end);
}

/**
 * 定義名からダウンロードファイル名を作る。使えない文字は `_` にする。
 * @param definitionName 画面の定義名。空なら `definition.yaml`。
 * @returns `.yaml` で終わるファイル名。
 */
export function buildDefinitionYamlDownloadFileName(definitionName: string): string {
  const replaced = definitionName.replace(UNSAFE_DOWNLOAD_NAME_CHARACTERS, "_");
  const trimmed = trimLeadingAndTrailingSpacesAndDots(replaced);
  if (trimmed.length === 0) {
    return DEFAULT_DOWNLOAD_FILE_NAME;
  }
  return `${trimmed}.yaml`;
}

/**
 * ローカルファイルを定義 YAML テキストとして読む。保存 API は呼ばない。
 * @param file 選択されたファイル。
 * @returns 適用できるテキスト、または拒否理由。
 */
export async function readDefinitionYamlFile(file: File): Promise<DefinitionYamlReadResult> {
  if (!isAllowedDefinitionYamlFileName(file.name)) {
    return { ok: false, reason: "extension" };
  }

  let rawText: string;
  try {
    rawText = await readFileAsUtf8(file);
  } catch {
    return { ok: false, reason: "readFailed" };
  }

  const text = stripUtf8Bom(rawText);
  switch (assertDefinitionYamlSize(text)) {
    case "ok":
      return { ok: true, text };
    case "tooLarge":
      return { ok: false, reason: "tooLarge" };
  }
}

/**
 * YAML テキストを一時アンカーでダウンロードし、オブジェクト URL を破棄する。
 * @param fileName `buildDefinitionYamlDownloadFileName` の結果。
 * @param yamlText 書き出す全文。
 */
export function downloadDefinitionYamlText(fileName: string, yamlText: string): void {
  const blob = new Blob([yamlText], { type: "text/yaml;charset=utf-8" });
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = objectUrl;
  anchor.download = fileName;
  anchor.rel = "noopener";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(objectUrl);
}
