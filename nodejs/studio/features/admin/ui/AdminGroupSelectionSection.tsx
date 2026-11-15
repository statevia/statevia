"use client";

import { useState } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { diffSelection, hasSelectionChanges } from "@/shared/lib/selectionDiff";
import { Button } from "@/shared/ui/Button";
import { Card } from "@/shared/ui/Card";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { SearchableCheckList, type SearchableCheckListItem } from "@/shared/ui/SearchableCheckList";

/** グループのメンバー欄・権限欄の入力。 */
export type AdminGroupSelectionSectionProps = {
  /** 欄の見出し（例: 「メンバー」）。 */
  title: string;
  /** 保存のボタンの文言（例: 「メンバーを保存」）。 */
  saveLabel: string;
  /** 確認ダイアログの見出し（例: 「メンバーの変更を保存しますか？」）。 */
  confirmTitle: string;
  /** 選べる項目。 */
  items: readonly SearchableCheckListItem[];
  /** 今の選択。 */
  selected: ReadonlySet<string>;
  /** 保存済みの選択。 */
  saved: ReadonlySet<string>;
  /** 保存中か。 */
  saving: boolean;
  /**
   * 選択を切り替える。
   * @param value 項目の値。
   */
  onToggle: (value: string) => void;
  /** 選択を保存済みの状態へ戻す。 */
  onReset: () => void;
  /**
   * 保存する。
   * @returns 保存できたら true（確認ダイアログを閉じる）。
   */
  onSave: () => Promise<boolean>;
};

/**
 * グループのメンバー・権限を、検索できる選択の部品で選び、保存前に追加と解除の差分を確かめてから保存する欄（要件 9.5）。
 * @remarks 変更が無いあいだは保存と「元に戻す」を押せない。保存を押すと確認ダイアログに追加と解除を並べ、決定で保存する。
 * 解除を含むときは権限を失う人が出るため、決定のボタンを赤にする。保存に失敗したらダイアログを開いたままにし、エラーはトーストで出す（呼び出し側）。
 */
export function AdminGroupSelectionSection({
  title,
  saveLabel,
  confirmTitle,
  items,
  selected,
  saved,
  saving,
  onToggle,
  onReset,
  onSave
}: Readonly<AdminGroupSelectionSectionProps>) {
  const uiText = useUiText();
  const text = uiText.admin.groupManagement;
  const [confirming, setConfirming] = useState(false);
  const diff = diffSelection(saved, selected);
  const changed = hasSelectionChanges(diff);
  const labelByValue = new Map(items.map((item) => [item.value, item.label]));
  const toLabels = (values: readonly string[]) => values.map((value) => labelByValue.get(value) ?? value);

  const handleConfirm = async () => {
    const ok = await onSave();
    if (ok) setConfirming(false);
  };

  return (
    <Card
      title={title}
      actions={
        <div className="flex flex-wrap items-center gap-2">
          {changed ? (
            <span className="text-xs text-md-on-surface-variant">{text.diffSummary(diff.added.length, diff.removed.length)}</span>
          ) : null}
          <Button variant="outline" disabled={!changed || saving} onClick={onReset}>
            {text.resetSelection}
          </Button>
          <Button variant="standard" disabled={!changed} loading={saving} onClick={() => setConfirming(true)}>
            {saving ? text.saving : saveLabel}
          </Button>
        </div>
      }
    >
      <SearchableCheckList ariaLabel={title} items={items} selected={selected} initial={saved} onToggle={onToggle} disabled={saving} />
      <ConfirmDialog
        open={confirming}
        title={confirmTitle}
        message={
          <div className="flex flex-col gap-3">
            {diff.added.length > 0 ? (
              <section>
                <h3 className="text-sm font-semibold text-md-on-surface">{text.diffAddedTitle(diff.added.length)}</h3>
                <ul aria-label={text.diffAddedTitle(diff.added.length)} className="mt-1 list-disc pl-5">
                  {toLabels(diff.added).map((label) => (
                    <li key={label}>{label}</li>
                  ))}
                </ul>
              </section>
            ) : null}
            {diff.removed.length > 0 ? (
              <section>
                <h3 className="text-sm font-semibold text-md-error">{text.diffRemovedTitle(diff.removed.length)}</h3>
                <ul aria-label={text.diffRemovedTitle(diff.removed.length)} className="mt-1 list-disc pl-5">
                  {toLabels(diff.removed).map((label) => (
                    <li key={label}>{label}</li>
                  ))}
                </ul>
              </section>
            ) : null}
          </div>
        }
        confirmLabel={text.confirmSave}
        confirmVariant={diff.removed.length > 0 ? "dangerSolid" : "lead"}
        loading={saving}
        onConfirm={() => void handleConfirm()}
        onCancel={() => setConfirming(false)}
      />
    </Card>
  );
}
