"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import type { ExecutionNodeDTO } from "../types";
import {
  buildNodeListGroups,
  NODE_LIST_PAGE_SIZE,
  nodeListPageForNode,
  pageNodeListGroups,
  type NodeListGroup
} from "../lib/nodeListGroups";
import { formatExecutionDuration } from "@/shared/lib/dateTime";
import { getStatusStyle } from "@/shared/lib/statusStyle";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { ListPagination } from "@/shared/ui/ListPagination";

type NodeListViewProps = {
  nodes: ExecutionNodeDTO[];
  selectedNodeId: string | null;
  onSelectNode: (nodeId: string) => void;
  /**
   * 1 ページの状態数。未指定時は {@link NODE_LIST_PAGE_SIZE}。
   * テストからページ境界を短くするための上書き。
   */
  pageSize?: number;
};

/** 一覧の「ノード名」列。名前が無いときはダッシュ。 */
function listNodeName(nodeName: string): string {
  return nodeName.length > 0 ? nodeName : "—";
}

/** 一覧の「実行時間」列。算出できないときはダッシュ。 */
function listDurationText(node: ExecutionNodeDTO): string {
  return formatExecutionDuration(node.startedAt, node.completedAt) ?? "—";
}

/**
 * 実行ノードの一覧。同じ状態名の訪問は 1 行にまとめ、状態数が多いときは画面内でページを切る。
 *
 * 行の材料は既に取得済みの実行ノードだけを使う。
 */
export function NodeListView({
  nodes,
  selectedNodeId,
  onSelectNode,
  pageSize = NODE_LIST_PAGE_SIZE
}: Readonly<NodeListViewProps>) {
  const uiText = useUiText();
  const groups = useMemo(() => buildNodeListGroups(nodes), [nodes]);
  const [page, setPage] = useState(1);
  const [expandedKeys, setExpandedKeys] = useState<ReadonlySet<string>>(() => new Set());
  const paged = pageNodeListGroups(groups, page, pageSize);

  useEffect(() => {
    if (paged.pageIndex !== page) setPage(paged.pageIndex);
  }, [page, paged.pageIndex]);

  useEffect(() => {
    if (!selectedNodeId) return;
    setPage(nodeListPageForNode(groups, selectedNodeId, pageSize));
  }, [groups, pageSize, selectedNodeId]);

  const toggleGroup = (key: string) => {
    setExpandedKeys((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  return (
    <div className="overflow-auto rounded-2xl border border-md-outline bg-md-surface p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold">{uiText.nodeList.title}</h2>
        <div className="flex items-center gap-3">
          <span className="text-xs text-md-on-surface-variant">
            {uiText.nodeList.summary(groups.length, nodes.length)}
          </span>
          {paged.pageCount > 1 && (
            <ListPagination
              ariaLabel={uiText.nodeList.pagination.ariaLabel}
              currentPageLabel={uiText.nodeList.pagination.currentPage(paged.pageIndex, paged.pageCount)}
              hasPrev={paged.pageIndex > 1}
              hasNext={paged.pageIndex < paged.pageCount}
              onPrev={() => setPage(paged.pageIndex - 1)}
              onNext={() => setPage(paged.pageIndex + 1)}
            />
          )}
        </div>
      </div>
      <table className="w-full table-fixed text-left text-sm">
        <thead className="text-xs text-md-on-surface-variant">
          <tr>
            <th className="w-8 py-2 pl-2 pr-0">
              <span className="sr-only">{uiText.nodeList.columns.expand}</span>
            </th>
            <th className="w-28 py-2 pl-2 pr-2">{uiText.nodeList.columns.status}</th>
            <th className="w-16 py-2 pl-2 pr-2">{uiText.nodeList.columns.type}</th>
            <th className="py-2 pl-2 pr-2">{uiText.nodeList.columns.nodeName}</th>
            <th className="w-32 py-2 pl-2 pr-2">{uiText.nodeList.columns.nodeId}</th>
            <th className="w-36 py-2 pl-2 pr-2">{uiText.nodeList.columns.duration}</th>
          </tr>
        </thead>
        <tbody>
          {paged.groups.map((group) => (
            <NodeListGroupRows
              key={group.key}
              group={group}
              expanded={expandedKeys.has(group.key)}
              selectedNodeId={selectedNodeId}
              onSelectNode={onSelectNode}
              onToggle={() => toggleGroup(group.key)}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

type NodeListGroupRowsProps = {
  group: NodeListGroup;
  expanded: boolean;
  selectedNodeId: string | null;
  onSelectNode: (nodeId: string) => void;
  onToggle: () => void;
};

/** 状態 1 件分の代表行と、展開した訪問行。 */
function NodeListGroupRows({
  group,
  expanded,
  selectedNodeId,
  onSelectNode,
  onToggle
}: Readonly<NodeListGroupRowsProps>) {
  const uiText = useUiText();
  const multiple = group.visits.length > 1;
  const name = listNodeName(group.nodeName);
  const showVisits = multiple && expanded;

  return (
    <>
      <NodeListDataRow
        node={group.representative}
        nodeName={name}
        selected={selectedNodeId === group.representative.nodeId}
        onSelect={() => onSelectNode(group.representative.nodeId)}
        toggle={multiple ? (
          <button
            type="button"
            className="inline-flex h-5 w-5 items-center justify-center text-md-on-surface-variant"
            aria-expanded={expanded}
            aria-label={expanded ? uiText.nodeList.collapseVisits(name) : uiText.nodeList.expandVisits(name)}
            onClick={(event) => {
              event.stopPropagation();
              onToggle();
            }}
          >
            <span className={`transition-transform ${expanded ? "rotate-90" : ""}`} aria-hidden>
              ▶
            </span>
          </button>
        ) : null}
      />
      {showVisits && group.visits.map((visit) => (
        <NodeListDataRow
          key={visit.nodeId}
          node={visit}
          nodeName={uiText.nodeList.visitLabel(name, visit.attempt)}
          selected={selectedNodeId === visit.nodeId}
          onSelect={() => onSelectNode(visit.nodeId)}
        />
      ))}
    </>
  );
}

type NodeListDataRowProps = {
  node: ExecutionNodeDTO;
  nodeName: string;
  selected: boolean;
  onSelect: () => void;
  toggle?: ReactNode;
};

/** 一覧の 1 行。選択は渡されたノード ID をそのまま使う。 */
function NodeListDataRow({
  node,
  nodeName,
  selected,
  onSelect,
  toggle = null
}: Readonly<NodeListDataRowProps>) {
  const style = getStatusStyle(node.status);
  const runningClass = node.status === "RUNNING" ? "opacity-80" : "";
  const selectedClass = selected ? "outline outline-2 outline-md-primary" : "";
  const durationText = listDurationText(node);

  return (
    <tr
      className={`cursor-pointer border-t border-md-outline ${style.bgClass} ${runningClass} ${selectedClass}`}
      onClick={onSelect}
    >
      <td className="w-6 py-2 pl-2 pr-0">
        {toggle ?? <span className="inline-block h-5 w-5" aria-hidden />}
      </td>
      <td className="py-2 pl-2 pr-2">
        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${style.badgeClass}`}>
          {node.status}
        </span>
      </td>
      <td className="py-2 pl-2 pr-2">{node.nodeType}</td>
      <td className="truncate py-2 pl-2 pr-2 font-mono text-xs" title={nodeName}>
        {nodeName}
      </td>
      <td className="truncate py-2 pl-2 pr-2 font-mono text-xs" title={node.nodeId}>{node.nodeId}</td>
      <td className="truncate py-2 pl-2 pr-2 font-mono text-xs" title={durationText}>{durationText}</td>
    </tr>
  );
}
