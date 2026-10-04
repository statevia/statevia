import { afterEach, describe, expect, it, vi } from "vitest";
import { DEFINITION_YAML_MAX_BYTES } from "@/shared/lib/validation/formRules";
import {
  assertDefinitionYamlSize,
  buildDefinitionYamlDownloadFileName,
  downloadDefinitionYamlText,
  isAllowedDefinitionYamlFileName,
  readDefinitionYamlFile,
  stripUtf8Bom
} from "@/features/definition-editor/lib/definitionYamlFile";

describe("definitionYamlFile", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("yaml と yml は大文字小文字を問わず許可し、それ以外は拒否する", () => {
    expect(isAllowedDefinitionYamlFileName("flow.YAML")).toBe(true);
    expect(isAllowedDefinitionYamlFileName("flow.Yml")).toBe(true);
    expect(isAllowedDefinitionYamlFileName("flow.txt")).toBe(false);
    expect(isAllowedDefinitionYamlFileName("flow.yaml.bak")).toBe(false);
  });

  it("先頭の UTF-8 BOM だけを除く", () => {
    expect(stripUtf8Bom("\uFEFFversion: 1\n")).toBe("version: 1\n");
    expect(stripUtf8Bom("version: 1\n")).toBe("version: 1\n");
  });

  it("UTF-8 が 256KB ちょうどなら許可し、1 バイト超は拒否する", () => {
    const atLimit = "a".repeat(DEFINITION_YAML_MAX_BYTES);
    const overLimit = "a".repeat(DEFINITION_YAML_MAX_BYTES + 1);

    expect(assertDefinitionYamlSize(atLimit)).toBe("ok");
    expect(assertDefinitionYamlSize(overLimit)).toBe("tooLarge");
  });

  it("定義名の使えない文字をアンダースコアにし、空なら definition.yaml にする", () => {
    expect(buildDefinitionYamlDownloadFileName("")).toBe("definition.yaml");
    expect(buildDefinitionYamlDownloadFileName("  ...  ")).toBe("definition.yaml");
    expect(buildDefinitionYamlDownloadFileName("a/b\\c:d*e?f\"g<h>i|j")).toBe("a_b_c_d_e_f_g_h_i_j.yaml");
    expect(buildDefinitionYamlDownloadFileName("  My Def. ")).toBe("My Def.yaml");
  });

  it("許可ファイルは BOM を除いたテキストを返す", async () => {
    const file = new File(["\uFEFFversion: 1\n"], "sample.yaml", { type: "text/yaml" });

    const result = await readDefinitionYamlFile(file);

    expect(result).toEqual({ ok: true, text: "version: 1\n" });
  });

  it("拡張子が違うファイルは読まない", async () => {
    const file = new File(["version: 1\n"], "sample.txt", { type: "text/plain" });

    const result = await readDefinitionYamlFile(file);

    expect(result).toEqual({ ok: false, reason: "extension" });
  });

  it("256KB を超えるファイルは tooLarge を返す", async () => {
    const file = new File(["a".repeat(DEFINITION_YAML_MAX_BYTES + 1)], "big.yaml", { type: "text/yaml" });

    const result = await readDefinitionYamlFile(file);

    expect(result).toEqual({ ok: false, reason: "tooLarge" });
  });

  it("読み取り失敗は readFailed を返す", async () => {
    vi.stubGlobal("FileReader", class {
      result: string | null = null;
      error = new DOMException("read");
      onload: (() => void) | null = null;
      onerror: (() => void) | null = null;
      readAsText(): void {
        this.onerror?.();
      }
    });
    const file = new File(["version: 1\n"], "sample.yaml", { type: "text/yaml" });

    const result = await readDefinitionYamlFile(file);

    expect(result).toEqual({ ok: false, reason: "readFailed" });
  });

  it("ダウンロードは現在テキストの Blob を作り、オブジェクト URL を破棄する", () => {
    const createObjectURL = vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:yaml");
    const revokeObjectURL = vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);

    downloadDefinitionYamlText("definition.yaml", "version: 1\n");

    expect(createObjectURL).toHaveBeenCalledTimes(1);
    const blob = createObjectURL.mock.calls[0]?.[0];
    expect(blob).toBeInstanceOf(Blob);
    expect(revokeObjectURL).toHaveBeenCalledWith("blob:yaml");
  });
});
