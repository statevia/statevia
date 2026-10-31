import type { DefinitionYamlReadFailureReason } from "../lib/definitionYamlFile";
import type { DefinitionEditorFeatureUiText } from "./types";

/** ローカル YAML の読み込み結果に対応するトースト種別。 */
export type DefinitionYamlFileToastOutcome = "loaded" | DefinitionYamlReadFailureReason;

/**
 * ローカル YAML 入出力のトースト文言を返す。
 * @param text definition-editor の文言。
 * @param outcome 読み込み成功、または拒否理由。
 * @returns 表示する文。
 */
export function definitionYamlFileToastMessage(
  text: DefinitionEditorFeatureUiText,
  outcome: DefinitionYamlFileToastOutcome
): string {
  switch (outcome) {
    case "loaded":
      return text.definitionEditor.toasts.yamlLoaded;
    case "extension":
      return text.definitionEditor.toasts.yamlExtensionRejected;
    case "tooLarge":
      return text.definitionEditor.validation.yamlTooLarge;
    case "readFailed":
      return text.definitionEditor.toasts.yamlReadFailed;
  }
}
