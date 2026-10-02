"use client";

import { useState } from "react";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { useDelayedVisibility } from "@/shared/lib/useDelayedVisibility";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import type { ExecutionEventWithSeq } from "../types";
import { useI18n } from "@/shared/i18n/uiTextContext";

function eventLabel(event: ExecutionEventWithSeq): string {
  switch (event.type) {
    case "GraphUpdated":
      return `GraphUpdated (${event.patch?.nodes?.length ?? 0} nodes)`;
    case "ExecutionStatusChanged":
      return `Status → ${event.to}`;
    case "NodeCancelled":
      return `NodeCancelled: ${event.nodeId}`;
    case "NodeFailed":
      return `NodeFailed: ${event.nodeId}`;
    default: {
      const e = event as { type?: string };
      return e.type ?? "Unknown";
    }
  }
}

function toErrorMessage(error: unknown, fallbackMessage: string): string {
  if (error instanceof Error) return error.message;
  if (typeof error === "string") return error;
  if (typeof error === "object" && error !== null && "message" in error) {
    return String((error).message);
  }
  return fallbackMessage;
}

type ExecutionTimelineProps = {
  events: ExecutionEventWithSeq[];
  loading: boolean;
  error: unknown;
  selectedSeq: number | null;
  onSelectSeq: (seq: number | null) => void;
  onBackToCurrent: () => void;
  isReplaying: boolean;
  hasMore?: boolean;
  loadingMore?: boolean;
  onLoadMore?: () => void;
};

/** 実行イベントのタイムライン一覧。 */
export function ExecutionTimeline({
  events,
  loading,
  error,
  selectedSeq,
  onSelectSeq,
  onBackToCurrent,
  isReplaying,
  hasMore = false,
  loadingMore = false,
  onLoadMore
}: Readonly<ExecutionTimelineProps>) {
  const { uiText, locale } = useI18n();
  const dateTimeLocale = getDateTimeLocale(locale);
  const [expanded, setExpanded] = useState(false);
  const showLoading = useDelayedVisibility(loading);

  return (
    <section className="rounded-2xl border border-md-outline bg-md-surface p-4 shadow-sm">
      <div className="flex items-center justify-between gap-2 border-b border-md-outline pb-2">
        <button
          type="button"
          onClick={() => setExpanded((e) => !e)}
          className="flex flex-1 cursor-pointer items-center gap-1.5 rounded-t-lg -mx-1 px-1 py-0.5 text-left hover:bg-md-surface-container-high"
          aria-expanded={expanded}
          aria-controls="execution-timeline-body"
          id="execution-timeline-heading"
        >
          <span
            className={`text-md-on-surface-variant transition-transform ${expanded ? "rotate-90" : ""}`}
            aria-hidden
          >
            ▶
          </span>
          <h2 id="execution-timeline-heading-text" className="text-sm font-semibold text-md-on-surface">
            {uiText.executionTimeline.title}
          </h2>
        </button>
        {isReplaying && (
          <button
            type="button"
            onClick={onBackToCurrent}
            className="shrink-0 rounded-lg border border-md-outline-variant bg-md-surface-container px-2 py-1 text-xs font-medium text-md-on-surface hover:bg-md-surface-container-high"
          >
            {uiText.executionTimeline.backToCurrent}
          </button>
        )}
      </div>

      <div
        id="execution-timeline-body"
        aria-labelledby="execution-timeline-heading-text"
        hidden={!expanded}
        className="mt-2"
      >
      {error ? (
        <p className="mt-2 text-xs text-red-600" role="alert">
          {toErrorMessage(error, uiText.executionTimeline.errorUnknown)}
        </p>
      ) : null}

      {showLoading && (
        <p className="mt-2 text-xs text-md-on-surface-variant">{uiText.actions.loading}</p>
      )}

      {!loading && !error && events.length === 0 && (
        <p className="mt-2 text-xs text-md-on-surface-variant">{uiText.executionTimeline.empty}</p>
      )}

      {!loading && events.length > 0 && (
        <>
          <ul className="mt-2 max-h-[280px] overflow-y-auto space-y-1">
            {events.map((ev) => {
              const isSelected = selectedSeq === ev.seq;
              return (
                <li key={ev.seq}>
                  <button
                    type="button"
                    onClick={() => onSelectSeq(isSelected ? null : ev.seq)}
                    className={`w-full rounded-lg border px-2 py-1.5 text-left text-xs transition-colors ${
                      isSelected
                        ? "border-md-outline bg-md-surface-container-high font-medium text-md-on-surface"
                        : "border-transparent text-md-on-surface-variant hover:bg-md-surface-container-high hover:text-md-on-surface"
                    }`}
                  >
                    <span className="font-mono text-md-on-surface-variant">#{ev.seq}</span>
                    <span className="ml-2">
                      {formatDateTimeLocalized(
                        ev.at,
                        dateTimeLocale,
                        { month: "numeric", day: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit" }
                      )}
                    </span>
                    <span className="ml-2">{eventLabel(ev)}</span>
                  </button>
                </li>
              );
            })}
          </ul>
          {hasMore && onLoadMore && (
            <div className="mt-2">
              <button
                type="button"
                onClick={onLoadMore}
                disabled={loadingMore}
                className="w-full rounded-lg border border-md-outline-variant bg-md-surface-container px-2 py-1.5 text-xs text-md-on-surface hover:bg-md-surface-container-high disabled:opacity-50"
              >
                {loadingMore ? uiText.actions.loading : uiText.executionTimeline.loadMore}
              </button>
            </div>
          )}
        </>
      )}
      </div>
    </section>
  );
}
