import { useEffect, useMemo, useRef, useState } from 'react';
import { useFollowupSequence } from '../../hooks/useFollowupSequence';
import { FollowupStageRow } from '../../components/FollowupSequence/FollowupStageRow';
import type {
  FollowupSequenceConfig,
  FollowupStageConfig,
  FollowupStageDraft,
  FollowupSequenceDraft,
} from '../../types/followupSequence';

// FEAT-EFS Drip Sequence — Dashboard editor page mounted at /settings/followup-sequence.
//
// MVP scope: single-sequence editor (matches Dent pilot's one-sequence-per-tenant
// pattern). Multi-sequence selector is intentionally out-of-scope — operator picks
// slug, edits stages, saves.
//
// Disabled UI guard (lessons 2026-04-22 P3 CQ9 FAIL): inputs are NOT disabled when
// the upstream load fails — operators retain access to existing data even if
// Marketing is transiently unreachable. They are only disabled DURING save.

const MAX_STAGES = 5;
const MAX_WINDOW = 30;

function emptyStage(): FollowupStageDraft {
  return {
    draftId: cryptoRandomId(),
    delayDaysInput: '3',
    templateSlug: '',
    templateGroup: '',
    rowError: null,
  };
}

function cryptoRandomId(): string {
  // Best-effort unique id for React keying; falls back to Date+Math when crypto
  // is unavailable (older browsers / test runners).
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function toDraft(seq: FollowupSequenceConfig | null): FollowupSequenceDraft {
  if (!seq) {
    return {
      slug: '',
      abSplitPercent: 50,
      enabled: false,
      stages: [emptyStage()],
      formError: null,
    };
  }
  return {
    slug: seq.slug,
    abSplitPercent: seq.ab_split_percent,
    enabled: seq.enabled,
    stages: seq.stages.map((s: FollowupStageConfig) => ({
      draftId: cryptoRandomId(),
      delayDaysInput: String(s.delay_days),
      templateSlug: s.template_slug,
      templateGroup: s.template_group ?? '',
      rowError: null,
    })),
    formError: null,
  };
}

function validateAndConvert(
  draft: FollowupSequenceDraft,
): { ok: true; config: FollowupSequenceConfig } | { ok: false; draft: FollowupSequenceDraft } {
  const next: FollowupSequenceDraft = {
    ...draft,
    formError: null,
    stages: draft.stages.map((s) => ({ ...s, rowError: null })),
  };
  // Slug shape — same as backend regex.
  if (!/^[a-z0-9][a-z0-9_-]{0,63}$/.test(draft.slug)) {
    next.formError = 'Slug gecersiz: lowercase harf/rakam ile baslamali, [a-z0-9_-] icerebilir, max 64 karakter.';
    return { ok: false, draft: next };
  }
  if (draft.abSplitPercent < 0 || draft.abSplitPercent > 100) {
    next.formError = 'A/B split 0-100 araliginda olmali.';
    return { ok: false, draft: next };
  }
  if (draft.stages.length === 0) {
    next.formError = 'En az 1 stage tanimlamalisiniz.';
    return { ok: false, draft: next };
  }
  if (draft.stages.length > MAX_STAGES) {
    next.formError = `Cap asimi: max ${MAX_STAGES} stage. Mevcut ${draft.stages.length}.`;
    return { ok: false, draft: next };
  }

  let cumulative = 0;
  let anyRowFail = false;
  next.stages = draft.stages.map((s) => {
    const delay = Number(s.delayDaysInput);
    if (!Number.isFinite(delay) || delay <= 0) {
      anyRowFail = true;
      return { ...s, rowError: 'delay_days 0\'dan buyuk bir tam sayi olmali.' };
    }
    if (delay > MAX_WINDOW) {
      anyRowFail = true;
      return { ...s, rowError: `delay_days max ${MAX_WINDOW} olabilir.` };
    }
    cumulative += delay;
    if (cumulative > MAX_WINDOW) {
      anyRowFail = true;
      return { ...s, rowError: `Cap asimi: kumulatif pencere max ${MAX_WINDOW}; bu stage sonunda ${cumulative}.` };
    }
    if (!/^[a-z0-9][a-z0-9_-]{0,63}$/.test(s.templateSlug)) {
      anyRowFail = true;
      return { ...s, rowError: 'template_slug gecersiz: [a-z0-9_-] kullanin, lowercase ile baslayin.' };
    }
    return { ...s, rowError: null };
  });

  if (anyRowFail) return { ok: false, draft: next };

  const config: FollowupSequenceConfig = {
    slug: draft.slug,
    ab_split_percent: draft.abSplitPercent,
    enabled: draft.enabled,
    stages: next.stages.map((s) => ({
      delay_days: Number(s.delayDaysInput),
      template_slug: s.templateSlug,
      template_group: s.templateGroup.trim() === '' ? null : s.templateGroup.trim(),
    })),
  };
  return { ok: true, config };
}

export function FollowupSequenceSettingsPage() {
  const {
    sequences,
    testMode,
    noReplyThresholdDays,
    isLoading,
    error,
    errorKind,
    refresh,
    upsert,
  } = useFollowupSequence(true);

  // Single-sequence editor MVP — pick the first sequence (or empty draft when none exists).
  // Multi-sequence selector is a future enhancement.
  const selectedSequence = sequences[0] ?? null;
  const [draft, setDraft] = useState<FollowupSequenceDraft>(() => toDraft(selectedSequence));
  const [isSaving, setIsSaving] = useState(false);
  const [saveBanner, setSaveBanner] = useState<string | null>(null);

  // Sync draft when the loaded sequence changes (e.g. after refresh).
  const lastLoadedSlugRef = useRef<string | null>(null);
  useEffect(() => {
    const loadedSlug = selectedSequence?.slug ?? null;
    if (loadedSlug !== lastLoadedSlugRef.current) {
      lastLoadedSlugRef.current = loadedSlug;
      setDraft(toDraft(selectedSequence));
    }
  }, [selectedSequence]);

  // Auto-clear save banner after 4s. Cleanup ref to prevent stale timer warnings
  // (lessons 2026-04-22 P3 CQ6).
  const bannerTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(() => {
    if (saveBanner === null) return;
    if (bannerTimerRef.current) clearTimeout(bannerTimerRef.current);
    bannerTimerRef.current = setTimeout(() => setSaveBanner(null), 4000);
    return () => {
      if (bannerTimerRef.current) {
        clearTimeout(bannerTimerRef.current);
        bannerTimerRef.current = null;
      }
    };
  }, [saveBanner]);

  // Existing-data guard: only disable inputs when (a) we are saving OR
  // (b) upstream load failed AND we have NO existing draft to edit.
  // This means a transient Marketing outage does not lock the UI when the
  // operator already has data loaded (lessons 2026-04-22 P3 CQ9 FAIL).
  const hasUsableDraft = draft.stages.length > 0;
  const inputsDisabled = isSaving || (errorKind === 'upstream_fail' && !hasUsableDraft);

  // testMode comes from the hook (backed by tenant_settings.efs_test_mode surfaced via
  // the Marketing GET envelope) — unit labels render "dk" when true, "gun" when false.
  // noReplyThresholdDays is shown in the page hints so operators know when the
  // NoReplyCheckJob would fire (informational only, not editable from this page).

  const handleAddStage = () => {
    if (draft.stages.length >= MAX_STAGES) return;
    setDraft((d) => ({ ...d, stages: [...d.stages, emptyStage()], formError: null }));
  };

  const handleStageChange = (index: number, next: FollowupStageDraft) => {
    setDraft((d) => ({
      ...d,
      stages: d.stages.map((s, i) => (i === index ? next : s)),
    }));
  };

  const handleStageRemove = (index: number) => {
    setDraft((d) => ({
      ...d,
      stages: d.stages.filter((_, i) => i !== index),
      formError: null,
    }));
  };

  const handleSave = async () => {
    const validation = validateAndConvert(draft);
    if (!validation.ok) {
      setDraft(validation.draft);
      return;
    }
    setIsSaving(true);
    setSaveBanner(null);
    try {
      await upsert(validation.config);
      setSaveBanner('Kaydedildi.');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Beklenmeyen hata.';
      setDraft((d) => ({ ...d, formError: message }));
    } finally {
      setIsSaving(false);
    }
  };

  const summary = useMemo(() => {
    const totalDelay = draft.stages.reduce((sum, s) => sum + (Number(s.delayDaysInput) || 0), 0);
    return {
      stageCount: draft.stages.length,
      totalDelay,
      drip: draft.abSplitPercent,
      control: 100 - draft.abSplitPercent,
    };
  }, [draft]);

  return (
    <div className="p-6 max-w-4xl">
      <h1 className="text-2xl font-semibold text-gray-900 mb-2">Follow-Up Sequence</h1>
      <p className="text-sm text-gray-600 mb-2">
        Lead cevap vermediginde / offer reddedildiginde otomatik gonderilen N-asamali
        nurture mesaj zinciri. A/B kontrol grubu deterministik atanir
        (sha256(tenant|lead|sequence) % 100).
      </p>
      <p className="text-xs text-gray-500 mb-4">
        No-reply threshold: <span className="font-mono">{noReplyThresholdDays}</span> gun
        (NoReplyCheckJob welcome enqueue'dan sonra bu kadar bekler, sonra inbound kontrol eder).
      </p>

      {testMode && (
        <div className="bg-amber-50 border border-amber-300 rounded p-3 mb-4 text-sm text-amber-900">
          <span className="font-semibold">TEST MODE aktif:</span> stage delay degerleri
          <span className="font-mono"> dakika </span>olarak yorumlaniyor (gun degil). Smoke
          sonrasi <span className="font-mono">tenant_settings.efs_test_mode</span> FALSE yapin.
        </div>
      )}

      {isLoading && (
        <div className="bg-blue-50 border border-blue-200 rounded p-3 mb-4 text-sm text-blue-800">
          Sequence listesi yukleniyor...
        </div>
      )}

      {error && (
        <div className="bg-red-50 border border-red-200 rounded p-3 mb-4 text-sm text-red-800">
          <span className="font-semibold">Yukleme hatasi:</span> {error.message}
          <button
            type="button"
            onClick={() => refresh()}
            className="ml-3 underline hover:no-underline"
          >
            Tekrar dene
          </button>
        </div>
      )}

      {saveBanner && (
        <div className="bg-green-50 border border-green-200 rounded p-3 mb-4 text-sm text-green-800">
          {saveBanner}
        </div>
      )}

      {draft.formError && (
        <div className="bg-red-50 border border-red-200 rounded p-3 mb-4 text-sm text-red-800">
          {draft.formError}
        </div>
      )}

      <div className="bg-white border border-gray-200 rounded p-4 mb-4">
        <div className="grid grid-cols-2 gap-4 mb-4">
          <label className="block">
            <span className="block text-xs font-medium text-gray-700 mb-1">Sequence Slug</span>
            <input
              type="text"
              value={draft.slug}
              onChange={(e) => setDraft((d) => ({ ...d, slug: e.target.value }))}
              disabled={inputsDisabled}
              placeholder="ornek: post-roadshow"
              className="w-full px-3 py-2 border border-gray-300 rounded text-sm font-mono disabled:bg-gray-100"
            />
          </label>
          <label className="block">
            <span className="block text-xs font-medium text-gray-700 mb-1">
              A/B Split (drip %): <span className="font-bold">{draft.abSplitPercent}%</span> drip / {100 - draft.abSplitPercent}% control
            </span>
            <input
              type="range"
              min={0}
              max={100}
              step={5}
              value={draft.abSplitPercent}
              onChange={(e) => setDraft((d) => ({ ...d, abSplitPercent: Number(e.target.value) }))}
              disabled={inputsDisabled}
              className="w-full"
            />
          </label>
        </div>

        <label className="inline-flex items-center cursor-pointer mb-4">
          <input
            type="checkbox"
            checked={draft.enabled}
            onChange={(e) => setDraft((d) => ({ ...d, enabled: e.target.checked }))}
            disabled={inputsDisabled}
            className="mr-2"
          />
          <span className="text-sm text-gray-700">Sequence aktif (enabled)</span>
        </label>

        <h2 className="text-sm font-semibold text-gray-800 mb-2">Stages</h2>
        <table className="w-full mb-3">
          <thead>
            <tr className="text-left text-xs font-medium text-gray-600 border-b border-gray-300">
              <th className="py-2 px-3">#</th>
              <th className="py-2 px-3">Delay</th>
              <th className="py-2 px-3">Template Slug</th>
              <th className="py-2 px-3">Template Group</th>
              <th className="py-2 px-3"></th>
            </tr>
          </thead>
          <tbody>
            {draft.stages.map((s, i) => (
              <FollowupStageRow
                key={s.draftId}
                index={i}
                draft={s}
                testMode={testMode}
                onChange={(next) => handleStageChange(i, next)}
                onRemove={() => handleStageRemove(i)}
                disabled={inputsDisabled}
              />
            ))}
          </tbody>
        </table>

        <button
          type="button"
          onClick={handleAddStage}
          disabled={inputsDisabled || draft.stages.length >= MAX_STAGES}
          className="text-xs text-blue-600 hover:text-blue-800 disabled:text-gray-400 disabled:cursor-not-allowed"
        >
          + Stage ekle ({draft.stages.length}/{MAX_STAGES})
        </button>
      </div>

      <div className="flex items-center justify-between mb-4">
        <div className="text-xs text-gray-600">
          {summary.stageCount} stage · toplam delay: {summary.totalDelay} {testMode ? 'dk' : 'gun'} · A/B {summary.drip}%/{summary.control}%
        </div>
        <button
          type="button"
          onClick={handleSave}
          disabled={inputsDisabled}
          className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded disabled:bg-gray-300"
        >
          {isSaving ? 'Kaydediliyor...' : 'Kaydet'}
        </button>
      </div>
    </div>
  );
}

export default FollowupSequenceSettingsPage;
