"use client";

import { useEffect, useRef, useState } from "react";
import type { DefinitionGraphWaitSubscribeEntry } from "../lib/types";

/** Wait `subscribe` 行編集 UI 向けの i18n 文言。 */
export type WaitSubscribeEditorLabels = {
  waitSubscribeSectionTitle: string;
  waitSubscribeTopicLabel: string;
  waitSubscribeKeyLabel: string;
  waitSubscribeNextLabel: string;
  waitSubscribeAdd: string;
  waitSubscribeRemove: string;
};

/**
 * `WaitSubscribeEditor` の props。
 *
 * @property entries 購読行
 * @property labels i18n 文言
 * @property disabled 競合時など編集不可にするとき true
 * @property onEntriesChange 配列確定時のコールバック
 */
export type WaitSubscribeEditorProps = {
  entries: readonly DefinitionGraphWaitSubscribeEntry[];
  labels: WaitSubscribeEditorLabels;
  disabled?: boolean;
  onEntriesChange: (entries: DefinitionGraphWaitSubscribeEntry[]) => void;
};

type WaitSubscribeRowDraft = {
  id: string;
  topic: string;
  key: string;
  next: string;
};

/**
 * Wait ノードの `subscribe`（topic / key / next）を行編集する。
 *
 * @param props.entries 現在の購読配列
 * @param props.labels i18n 文言
 * @param props.disabled 競合時など編集不可にするとき true
 * @param props.onEntriesChange 配列確定時のコールバック
 */
export function WaitSubscribeEditor({
  entries,
  labels,
  disabled = false,
  onEntriesChange
}: Readonly<WaitSubscribeEditorProps>) {
  const [rows, setRows] = useState<WaitSubscribeRowDraft[]>(() => syncRowsFromEntries(entries, []));
  const rowsRef = useRef(rows);
  rowsRef.current = rows;

  useEffect(() => {
    const currentEntries = toEntries(rowsRef.current);
    if (sameSubscribeEntries(currentEntries, entries)) {
      return;
    }
    setRows(syncRowsFromEntries(entries, rowsRef.current));
  }, [entries]);

  const commitCurrentRows = () => {
    onEntriesChange(toEntries(rowsRef.current));
  };

  const patchRow = (rowId: string, patch: Partial<Pick<WaitSubscribeRowDraft, "topic" | "key" | "next">>) => {
    setRows((current) => {
      const next = current.map((entry) => (entry.id === rowId ? { ...entry, ...patch } : entry));
      rowsRef.current = next;
      return next;
    });
  };

  const removeRow = (rowId: string) => {
    const nextRows = rowsRef.current.filter((entry) => entry.id !== rowId);
    setRows(nextRows);
    onEntriesChange(toEntries(nextRows));
  };

  const addRow = () => {
    const nextRows = [...rowsRef.current, { id: createRowId(), topic: "", key: "", next: "" }];
    setRows(nextRows);
    onEntriesChange(toEntries(nextRows));
  };

  return (
    <div className="space-y-2">
      <p className="text-xs font-medium">{labels.waitSubscribeSectionTitle}</p>
      {rows.map((row) => (
        <WaitSubscribeRowEditor
          key={row.id}
          row={row}
          labels={labels}
          disabled={disabled}
          onPatch={(patch) => {
            patchRow(row.id, patch);
          }}
          onCommit={commitCurrentRows}
          onRemove={() => {
            removeRow(row.id);
          }}
        />
      ))}
      <button
        type="button"
        className="rounded border border-[var(--md-sys-color-outline-variant)] px-2 py-1 text-xs disabled:opacity-50"
        disabled={disabled}
        onClick={addRow}
      >
        {labels.waitSubscribeAdd}
      </button>
    </div>
  );
}

type WaitSubscribeRowEditorProps = {
  row: WaitSubscribeRowDraft;
  labels: WaitSubscribeEditorLabels;
  disabled: boolean;
  onPatch: (patch: Partial<Pick<WaitSubscribeRowDraft, "topic" | "key" | "next">>) => void;
  onCommit: () => void;
  onRemove: () => void;
};

/**
 * Wait `subscribe` の 1 行分の入力 UI。
 *
 * @param props.row 行ドラフト
 * @param props.labels i18n 文言
 * @param props.disabled 編集不可
 * @param props.onPatch フィールド更新
 * @param props.onCommit blur / Enter 時の確定
 * @param props.onRemove 行削除
 */
function WaitSubscribeRowEditor({
  row,
  labels,
  disabled,
  onPatch,
  onCommit,
  onRemove
}: Readonly<WaitSubscribeRowEditorProps>) {
  return (
    <div className="space-y-1 rounded border border-[var(--md-sys-color-outline-variant)] p-2">
      <label className="block text-xs">
        <span className="block">{labels.waitSubscribeTopicLabel}</span>
        <input
          className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
          value={row.topic}
          disabled={disabled}
          onChange={(changeEvent) => {
            onPatch({ topic: changeEvent.target.value });
          }}
          onBlur={onCommit}
          onKeyDown={(keydownEvent) => {
            if (keydownEvent.key === "Enter") {
              keydownEvent.currentTarget.blur();
            }
          }}
        />
      </label>
      <label className="block text-xs">
        <span className="block">{labels.waitSubscribeKeyLabel}</span>
        <input
          className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
          value={row.key}
          disabled={disabled}
          onChange={(changeEvent) => {
            onPatch({ key: changeEvent.target.value });
          }}
          onBlur={onCommit}
          onKeyDown={(keydownEvent) => {
            if (keydownEvent.key === "Enter") {
              keydownEvent.currentTarget.blur();
            }
          }}
        />
      </label>
      <label className="block text-xs">
        <span className="block">{labels.waitSubscribeNextLabel}</span>
        <input
          className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
          value={row.next}
          disabled={disabled}
          onChange={(changeEvent) => {
            onPatch({ next: changeEvent.target.value });
          }}
          onBlur={onCommit}
          onKeyDown={(keydownEvent) => {
            if (keydownEvent.key === "Enter") {
              keydownEvent.currentTarget.blur();
            }
          }}
        />
      </label>
      <button
        type="button"
        className="rounded border border-[var(--md-sys-color-outline-variant)] px-2 py-1 text-xs disabled:opacity-50"
        disabled={disabled}
        onClick={onRemove}
      >
        {labels.waitSubscribeRemove}
      </button>
    </div>
  );
}

/**
 * 購読配列から行ドラフトを組み立てる。同じ index の既存行があれば id を引き継ぐ。
 *
 * @param entries 購読行
 * @param previousRows 直前の行（props 同期時の key 安定化用）
 * @returns 行ドラフト一覧
 */
function syncRowsFromEntries(
  entries: readonly DefinitionGraphWaitSubscribeEntry[],
  previousRows: readonly WaitSubscribeRowDraft[]
): WaitSubscribeRowDraft[] {
  return entries.map((entry, index) => ({
    id: previousRows[index]?.id ?? createRowId(),
    topic: entry.topic,
    key: entry.key ?? "",
    next: entry.next
  }));
}

function toEntries(rows: WaitSubscribeRowDraft[]): DefinitionGraphWaitSubscribeEntry[] {
  return rows.map((row) => {
    const key = row.key.trim();
    if (key.length === 0) {
      return { topic: row.topic, next: row.next };
    }
    return { topic: row.topic, key, next: row.next };
  });
}

function sameSubscribeEntries(
  left: readonly DefinitionGraphWaitSubscribeEntry[],
  right: readonly DefinitionGraphWaitSubscribeEntry[]
): boolean {
  if (left.length !== right.length) {
    return false;
  }
  return left.every((entry, index) => {
    const other = right[index];
    return entry.topic === other.topic && (entry.key ?? "") === (other.key ?? "") && entry.next === other.next;
  });
}

function createRowId(): string {
  return crypto.randomUUID();
}
