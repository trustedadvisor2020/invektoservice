import React from 'react';
import { Cpu, Server, Zap, Smartphone } from 'lucide-react';
import Badge from './Badge';

const ChatPreview = ({ steps = [], assistantName = 'Destek Asistani', assistantIcon: AssistantIcon = Smartphone }) => {
    return (
        <div className="bg-gray-100 rounded-2xl overflow-hidden border border-brand-100 shadow-sm flex flex-col h-[800px]">
            {/* Chat Header */}
            <div className="bg-surface p-5 border-b border-brand-100 flex items-center justify-between shadow-sm z-10">
                <div className="flex items-center gap-4">
                    <div className="w-12 h-12 rounded-full bg-emerald-500 overflow-hidden flex items-center justify-center text-white font-bold">
                        <AssistantIcon size={24} />
                    </div>
                    <div>
                        <h4 className="font-bold text-t-primary text-base">{assistantName}</h4>
                        <span className="text-sm text-emerald-600 font-medium">Cevrimici</span>
                    </div>
                </div>
                <Badge color="gray">Canli Onizleme</Badge>
            </div>

            {/* Chat Content */}
            <div className="flex-1 p-8 overflow-y-auto space-y-8 bg-gray-50">
                {steps.map((step, idx) => {
                    if (step.role === 'system' || step.role === 'ai') {
                        return (
                            <div key={idx} className="flex justify-center my-4">
                                <div className={`
                                    max-w-[90%] px-4 py-2 rounded-lg text-sm font-bold text-center border shadow-sm flex items-center gap-2
                                    ${step.role === 'ai' ? 'bg-brand-100 text-brand-800 border-brand-200' : 'bg-amber-50 text-amber-800 border-amber-200'}
                                `}>
                                    {step.role === 'ai' ? <Cpu size={16} /> : <Server size={16} />}
                                    {step.content}
                                </div>
                            </div>
                        );
                    }
                    if (step.role === 'agent') {
                        return (
                            <div key={idx} className="flex justify-center my-4">
                                <div className="max-w-[90%] px-4 py-2 rounded-lg text-sm font-bold text-center border shadow-sm flex items-center gap-2 bg-rose-50 text-rose-800 border-rose-200">
                                    <Server size={16} />
                                    {step.content}
                                </div>
                            </div>
                        );
                    }
                    return (
                        <div key={idx} className={`flex w-full ${step.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                            <div className={`
                                max-w-[80%] p-4 rounded-xl text-base shadow-sm relative leading-relaxed
                                ${step.role === 'user' ? 'bg-[#d9fdd3] text-gray-900 rounded-tr-none' : 'bg-white text-gray-900 rounded-tl-none'}
                            `}>
                                {step.content}
                                <span className="block text-xs text-gray-400 text-right mt-2">
                                    14:{String(30 + idx).padStart(2, '0')} {step.role === 'user' && '\u2713\u2713'}
                                </span>
                                <svg className={`absolute top-0 w-4 h-4 ${step.role === 'user' ? '-right-3 fill-[#d9fdd3]' : '-left-3 fill-white'}`} viewBox="0 0 10 10">
                                    <path d={step.role === 'user' ? "M0 0 L10 0 L0 10 Z" : "M0 0 L10 0 L10 10 Z"} />
                                </svg>
                            </div>
                        </div>
                    );
                })}
            </div>

            {/* Chat Input Placeholder */}
            <div className="p-4 bg-gray-50 border-t border-gray-200 flex items-center gap-3">
                <div className="p-2 text-gray-400 hover:text-gray-600 cursor-pointer"><Zap size={24} /></div>
                <div className="flex-1 bg-white border border-gray-200 rounded-full px-6 py-3 text-base text-gray-400 shadow-sm font-sans">
                    Mesaj yazin...
                </div>
                <div className="p-2 text-gray-400 hover:text-gray-600 cursor-pointer"><Smartphone size={24} /></div>
            </div>
        </div>
    );
};

export default ChatPreview;
