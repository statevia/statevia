"use client";

import { useCallback, useEffect, useState } from "react";
import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import {
  buildActionBindingUpdateBody,
  parseAllowedActionIds,
  type TenantActionBinding
} from "../actionBindings";

/**
 * Action バインディング管理画面の状態。
 * @returns 一覧、作成、更新、削除。
 */
export function useAdminActionBindingsPage() {
  const [bindings, setBindings] = useState<TenantActionBinding[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [kind, setKind] = useState<"variable" | "secret">("secret");
  const [value, setValue] = useState("");
  const [allowedActionIdsText, setAllowedActionIdsText] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [draftValues, setDraftValues] = useState<Record<string, string>>({});
  const [draftActions, setDraftActions] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setLoading(true);
    setErrorMessage(null);
    try {
      const listed = await apiGet<TenantActionBinding[]>("/admin/action-bindings");
      setBindings(listed);
      setDraftActions(
        Object.fromEntries(listed.map((binding) => [binding.name, binding.allowedActionIds.join("\n")]))
      );
      setDraftValues({});
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Failed to load action bindings.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const createBinding = useCallback(async () => {
    setSubmitting(true);
    setErrorMessage(null);
    try {
      await apiPost<TenantActionBinding>("/admin/action-bindings", {
        name: name.trim(),
        kind,
        value,
        allowedActionIds: parseAllowedActionIds(allowedActionIdsText)
      });
      setName("");
      setValue("");
      setAllowedActionIdsText("");
      await load();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Failed to create action binding.");
    } finally {
      setSubmitting(false);
    }
  }, [allowedActionIdsText, kind, load, name, value]);

  const saveBinding = useCallback(
    async (binding: TenantActionBinding) => {
      setSubmitting(true);
      setErrorMessage(null);
      try {
        const nextValue = draftValues[binding.name] ?? "";
        await apiPut<TenantActionBinding>(
          `/admin/action-bindings/${encodeURIComponent(binding.name)}`,
          buildActionBindingUpdateBody(
            binding.kind,
            binding.kind === "variable" ? (draftValues[binding.name] ?? binding.value ?? "") : nextValue,
            parseAllowedActionIds(draftActions[binding.name] ?? binding.allowedActionIds.join("\n"))
          )
        );
        await load();
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "Failed to update action binding.");
      } finally {
        setSubmitting(false);
      }
    },
    [draftActions, draftValues, load]
  );

  const deleteBinding = useCallback(
    async (bindingName: string) => {
      setSubmitting(true);
      setErrorMessage(null);
      try {
        await apiDelete(`/admin/action-bindings/${encodeURIComponent(bindingName)}`);
        await load();
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "Failed to delete action binding.");
      } finally {
        setSubmitting(false);
      }
    },
    [load]
  );

  return {
    bindings,
    loading,
    errorMessage,
    name,
    setName,
    kind,
    setKind,
    value,
    setValue,
    allowedActionIdsText,
    setAllowedActionIdsText,
    submitting,
    draftValues,
    setDraftValues,
    draftActions,
    setDraftActions,
    createBinding,
    saveBinding,
    deleteBinding
  };
}
