import { useState } from 'react';
import { X } from 'lucide-react';
import { api, FaqDto } from '../../lib/api';

interface Props {
  tenantId: number;
  faq?: FaqDto;
  onClose: () => void;
  onSave: () => void;
}

export function FaqEditModal({ tenantId, faq, onClose, onSave }: Props) {
  const isEdit = !!faq;
  const [question, setQuestion] = useState(faq?.question ?? '');
  const [answer, setAnswer] = useState(faq?.answer ?? '');
  const [category, setCategory] = useState(faq?.category ?? '');
  const [lang, setLang] = useState(faq?.lang ?? 'tr');
  const [keywords, setKeywords] = useState(faq?.keywords.join(', ') ?? '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSave = async () => {
    if (!question.trim() || !answer.trim()) {
      setError('Question and answer are required');
      return;
    }

    setSaving(true);
    setError(null);

    const kw = keywords.split(',').map(k => k.trim()).filter(Boolean);

    try {
      if (isEdit) {
        await api.updateFaq(tenantId, faq!.id, {
          question: question.trim(),
          answer: answer.trim(),
          category: category.trim() || undefined,
          lang,
          keywords: kw,
        });
      } else {
        await api.createFaq(tenantId, {
          question: question.trim(),
          answer: answer.trim(),
          category: category.trim() || undefined,
          lang,
          keywords: kw,
        });
      }
      onSave();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-lg mx-4">
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-slate-200">
          <h3 className="text-sm font-medium text-slate-800">
            {isEdit ? 'Edit FAQ' : 'Create FAQ'}
          </h3>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Form */}
        <div className="p-4 space-y-3">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Question *</label>
            <input
              type="text"
              value={question}
              onChange={e => setQuestion(e.target.value)}
              className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
              placeholder="What is the return policy?"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Answer *</label>
            <textarea
              value={answer}
              onChange={e => setAnswer(e.target.value)}
              rows={4}
              className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500 resize-y"
              placeholder="Our return policy allows..."
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Category</label>
              <input
                type="text"
                value={category}
                onChange={e => setCategory(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md"
                placeholder="general"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Language</label>
              <select
                value={lang}
                onChange={e => setLang(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md"
              >
                <option value="tr">Turkish</option>
                <option value="en">English</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Keywords (comma separated)</label>
            <input
              type="text"
              value={keywords}
              onChange={e => setKeywords(e.target.value)}
              className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md"
              placeholder="return, refund, policy"
            />
          </div>

          {error && <p className="text-sm text-red-600">{error}</p>}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-2 px-4 py-3 border-t border-slate-200">
          <button
            onClick={onClose}
            className="px-4 py-1.5 text-sm text-slate-600 bg-slate-100 rounded-md hover:bg-slate-200 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="px-4 py-1.5 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            {saving ? 'Saving...' : isEdit ? 'Update' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
}
