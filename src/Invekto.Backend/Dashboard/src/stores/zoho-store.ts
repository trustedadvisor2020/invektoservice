// Adim 3 Paket 3-B2: Dashboard Zoho UI state (connection + stage mappings read-only + sync log).
// Pattern mirror: flow-log-store.ts (Zustand + api.ts wrapper + error string).
// Error code conventions:
//   INV-INT-FE-xxx: frontend-display fallback codes (kullanilir yalnizca backend ApiClientError.error_code
//   yoksa veya mesaj cikmazsa). Backend kaynakli hatalarda ApiClientError mesaji direkt kullanicaya gosterilir.
import { create } from 'zustand';
import { api } from '../lib/api';
import type {
  ZohoConnectionStatusDto,
  ZohoStageMappingDto,
  ZohoSyncLogEntryDto,
  ZohoSyncLogQuery,
  ZohoSyncLogStatus,
} from '../lib/api';

export interface SyncLogFilters {
  status: ZohoSyncLogStatus | '';
  event: string;
  from: string;
  to: string;
}

const DEFAULT_FILTERS: SyncLogFilters = { status: '', event: '', from: '', to: '' };

export interface ZohoState {
  connection: ZohoConnectionStatusDto | null;
  connectionLoading: boolean;
  connectionError: string | null;

  stageMappings: ZohoStageMappingDto[] | null;
  stageMappingsLoading: boolean;
  stageMappingsError: string | null;

  syncLogItems: ZohoSyncLogEntryDto[];
  syncLogPage: number;
  syncLogPageSize: number;
  syncLogTotalCount: number;
  syncLogFilters: SyncLogFilters;
  syncLogLoading: boolean;
  syncLogError: string | null;
  syncLogRetryingId: number | null;

  loadConnection: () => Promise<void>;
  disconnect: () => Promise<{ tokenRevoked: boolean }>;
  loadStageMappings: () => Promise<void>;
  loadSyncLog: (overrides?: Partial<SyncLogFilters> & { page?: number; pageSize?: number }) => Promise<void>;
  updateSyncLogFilters: (patch: Partial<SyncLogFilters>) => void;
  resetSyncLogFilters: () => void;
  retrySyncLog: (id: number) => Promise<void>;
}

function errorMessage(err: unknown, fallback: string): string {
  if (err instanceof Error && err.message) return err.message;
  return fallback;
}

export const useZohoStore = create<ZohoState>((set, get) => ({
  connection: null,
  connectionLoading: false,
  connectionError: null,

  stageMappings: null,
  stageMappingsLoading: false,
  stageMappingsError: null,

  syncLogItems: [],
  syncLogPage: 1,
  syncLogPageSize: 20,
  syncLogTotalCount: 0,
  syncLogFilters: { ...DEFAULT_FILTERS },
  syncLogLoading: false,
  syncLogError: null,
  syncLogRetryingId: null,

  loadConnection: async () => {
    set({ connectionLoading: true, connectionError: null });
    try {
      const connection = await api.getZohoConnection();
      set({ connection, connectionLoading: false });
    } catch (err) {
      set({
        connectionError: errorMessage(err, '[INV-INT-FE-001] Zoho baglanti durumu alinamadi. Sayfayi yenileyin veya birkac dakika sonra tekrar deneyin.'),
        connectionLoading: false,
      });
    }
  },

  disconnect: async () => {
    const res = await api.disconnectZoho();
    // Local state'i temizle — connected=false konumuna gec.
    set({
      connection: { connected: false },
      connectionError: null,
    });
    return { tokenRevoked: res.tokenRevoked };
  },

  loadStageMappings: async () => {
    set({ stageMappingsLoading: true, stageMappingsError: null });
    try {
      const res = await api.getZohoStageMappings();
      set({ stageMappings: res.mappings, stageMappingsLoading: false });
    } catch (err) {
      set({
        stageMappingsError: errorMessage(err, '[INV-INT-FE-002] Asama eslesmeleri yuklenemedi. Sayfayi yenileyin.'),
        stageMappingsLoading: false,
      });
    }
  },

  loadSyncLog: async (overrides) => {
    const state = get();
    const filters: SyncLogFilters = {
      ...state.syncLogFilters,
      ...(overrides ?? {}),
    };
    const page = overrides?.page ?? state.syncLogPage;
    const pageSize = overrides?.pageSize ?? state.syncLogPageSize;

    set({ syncLogLoading: true, syncLogError: null, syncLogFilters: filters, syncLogPage: page, syncLogPageSize: pageSize });

    const query: ZohoSyncLogQuery = { page, pageSize };
    if (filters.status) query.status = filters.status;
    if (filters.event) query.event = filters.event;
    if (filters.from) query.from = filters.from;
    if (filters.to) query.to = filters.to;

    try {
      const res = await api.getZohoSyncLog(query);
      set({
        syncLogItems: res.items,
        syncLogPage: res.page,
        syncLogPageSize: res.pageSize,
        syncLogTotalCount: res.totalCount,
        syncLogLoading: false,
      });
    } catch (err) {
      set({
        syncLogError: errorMessage(err, '[INV-INT-FE-003] Senkron kaydi yuklenemedi. Filtreleri sifirlayip tekrar deneyin.'),
        syncLogLoading: false,
      });
    }
  },

  updateSyncLogFilters: (patch) => {
    set({ syncLogFilters: { ...get().syncLogFilters, ...patch } });
  },

  resetSyncLogFilters: () => {
    set({ syncLogFilters: { ...DEFAULT_FILTERS }, syncLogPage: 1 });
  },

  retrySyncLog: async (id: number) => {
    set({ syncLogRetryingId: id });
    try {
      await api.retryZohoSyncLog(id);
      // Mevcut filtre+sayfa ile yenile; row status='pending'e gecer.
      await get().loadSyncLog();
    } finally {
      set({ syncLogRetryingId: null });
    }
  },
}));
