import type { FlowConfigV2 } from './flow';

export interface WizardMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  flow_config_snapshot?: FlowConfigV2;
}

export interface WizardStreamEvent {
  type: 'text' | 'done' | 'error';
  content?: string;
  flow_config?: FlowConfigV2;
  prerequisites?: FlowPrerequisite[];
}

export interface FlowPrerequisite {
  type: 'action_required' | 'configuration' | 'integration';
  title: string;
  description: string;
  action: string;
}

export interface WizardState {
  flow_id: number;
  flow_name: string;
  wizard_status: 'drafting' | 'completed' | null;
  wizard_history: WizardMessage[];
  flow_config?: FlowConfigV2;
}
