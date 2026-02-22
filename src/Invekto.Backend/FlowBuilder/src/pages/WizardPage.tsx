import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import { useWizardStore } from '../store/wizard-store';
import { WizardChat } from '../components/WizardChat';
import { WizardPreview } from '../components/WizardPreview';

export function WizardPage() {
  const { flowId } = useParams<{ flowId: string }>();
  const navigate = useNavigate();
  const { session } = useAuth();
  const tenantId = session?.tenant_id ?? 0;

  const loadWizard = useWizardStore(s => s.loadWizard);
  const confirmFlow = useWizardStore(s => s.confirmFlow);
  const reset = useWizardStore(s => s.reset);
  const wizardStatus = useWizardStore(s => s.wizardStatus);
  const currentFlowPreview = useWizardStore(s => s.currentFlowPreview);
  const isStreaming = useWizardStore(s => s.isStreaming);
  const error = useWizardStore(s => s.error);
  const flowName = useWizardStore(s => s.flowName);
  const setFlowName = useWizardStore(s => s.setFlowName);

  const [nameInput, setNameInput] = useState('');
  const [confirming, setConfirming] = useState(false);

  useEffect(() => {
    if (flowId && tenantId) {
      loadWizard(tenantId, parseInt(flowId, 10));
    }
    return () => reset();
  }, [flowId, tenantId, loadWizard, reset]);

  useEffect(() => {
    if (flowName && !nameInput) setNameInput(flowName);
  }, [flowName, nameInput]);

  const handleBack = () => {
    navigate('/');
  };

  const handleConfirm = async () => {
    const name = nameInput.trim() || 'AI Akisi';
    setConfirming(true);
    try {
      await confirmFlow(name);
      navigate(`/editor/${flowId}`);
    } catch (_e) {
      // Error state is set inside confirmFlow in the wizard store
      console.warn('Wizard confirm failed — error visible in UI');
    } finally {
      setConfirming(false);
    }
  };

  const handleGoToEditor = () => {
    navigate(`/editor/${flowId}`);
  };

  const canConfirm = currentFlowPreview != null && !isStreaming && wizardStatus !== 'completed';
  const isCompleted = wizardStatus === 'completed';

  return (
    <div className="h-screen flex flex-col bg-navy-50">
      {/* Header */}
      <header className="bg-white border-b border-navy-100 px-4 py-2.5 flex items-center gap-3 flex-shrink-0">
        <button
          onClick={handleBack}
          className="p-1.5 rounded-lg text-navy-400 hover:text-navy-700 hover:bg-navy-50 transition-colors"
          title="Geri don"
        >
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18" />
          </svg>
        </button>

        <div className="flex items-center gap-2 flex-1 min-w-0">
          <div className="w-7 h-7 rounded-lg bg-purple-100 flex items-center justify-center flex-shrink-0">
            <svg className="w-4 h-4 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
            </svg>
          </div>
          <input
            type="text"
            value={nameInput}
            onChange={e => setNameInput(e.target.value)}
            placeholder="Akis adi..."
            className="flex-1 min-w-0 bg-transparent text-sm font-medium text-navy-900 placeholder:text-navy-300 focus:outline-none"
          />
        </div>

        {/* Status badge */}
        {isStreaming && (
          <span className="px-2.5 py-1 text-xs bg-purple-50 text-purple-600 rounded-full flex items-center gap-1.5 flex-shrink-0">
            <span className="w-1.5 h-1.5 rounded-full bg-purple-400 animate-pulse" />
            AI dusunuyor
          </span>
        )}

        {isCompleted ? (
          <button
            onClick={handleGoToEditor}
            className="px-4 py-2 bg-brand-500 hover:bg-brand-600 text-white text-sm font-medium rounded-lg transition-colors flex-shrink-0"
          >
            Editore Git
          </button>
        ) : (
          <button
            onClick={handleConfirm}
            disabled={!canConfirm || confirming}
            className="px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-medium rounded-lg transition-colors disabled:opacity-40 disabled:cursor-not-allowed flex-shrink-0"
          >
            {confirming ? 'Olusturuluyor...' : 'Akisi Olustur'}
          </button>
        )}
      </header>

      {/* Error banner */}
      {error && (
        <div className="bg-red-50 border-b border-red-200 px-4 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Split layout */}
      <div className="flex-1 flex min-h-0">
        {/* Chat panel (left) */}
        <div className="w-1/2 border-r border-navy-100 flex flex-col min-h-0">
          <WizardChat />
        </div>

        {/* Preview panel (right) */}
        <div className="w-1/2 flex flex-col min-h-0">
          <WizardPreview />
        </div>
      </div>
    </div>
  );
}
