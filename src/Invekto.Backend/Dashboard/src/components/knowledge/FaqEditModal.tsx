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
      setError('Soru ve cevap zorunludur');
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
      setError(err instanceof Error ? err.message : 'Kayit basarisiz');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="bg-white rounded-lg shadow-xl w-full max-w-lg mx-4">
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-navy-100">
          <h3 className="text-sm font-medium text-navy-900">
            {isEdit ? 'SSS Duzenle' : 'SSS Olustur'}
          </h3>
          <button onClick={onClose} className="text-navy-300 hover:text-navy-500">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Form */}
        <div className="p-4 space-y-3">
          <div>
            <label className="block text-xs font-medium text-navy-500 mb-1">Soru *</label>
            <input
              type="text"
              value={question}
              onChange={e => setQuestion(e.target.value)}
              className="w-full px-3 py-2 text-sm border border-navy-200 rounded-md focus:outline-none focus:ring-1 focus:ring-brand-500"
              placeholder="Iade politikasi nedir?"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-navy-500 mb-1">Cevap *</label>
            <textarea
              value={answer}
              onChange={e => setAnswer(e.target.value)}
              rows={4}
              className="w-full px-3 py-2 text-sm border border-navy-200 rounded-md focus:outline-none focus:ring-1 focus:ring-brand-500 resize-y"
              placeholder="Iade politikamiz..."
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-navy-500 mb-1">Kategori</label>
              <input
                type="text"
                value={category}
                onChange={e => setCategory(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-navy-200 rounded-md"
                placeholder="genel"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-navy-500 mb-1">Dil</label>
              <select
                value={lang}
                onChange={e => setLang(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-navy-200 rounded-md"
              >
                <option value="tr">Turkce</option>
                <option value="en">Ingilizce</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-navy-500 mb-1">Anahtar Kelimeler (virgul ile ayirin)</label>
            <input
              type="text"
              value={keywords}
              onChange={e => setKeywords(e.target.value)}
              className="w-full px-3 py-2 text-sm border border-navy-200 rounded-md"
              placeholder="iade, geri odeme, politika"
            />
          </div>

          {error && <p className="text-sm text-red-600">{error}</p>}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-2 px-4 py-3 border-t border-navy-100">
          <button
            onClick={onClose}
            className="px-4 py-1.5 text-sm text-navy-500 bg-navy-100 rounded-md hover:bg-navy-100 transition-colors"
          >
            Iptal
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="px-4 py-1.5 text-sm bg-brand-500 text-white rounded-md hover:bg-brand-600 disabled:opacity-50 transition-colors"
          >
            {saving ? 'Kaydediliyor...' : isEdit ? 'Guncelle' : 'Olustur'}
          </button>
        </div>
      </div>
    </div>
  );
}
