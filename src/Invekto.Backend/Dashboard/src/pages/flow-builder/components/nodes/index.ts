import type { NodeTypes } from '@xyflow/react';
import { TriggerStartNode } from './TriggerStartNode';
import { WebhookTriggerNode } from './WebhookTriggerNode';
import { OutboundTriggerNode } from './OutboundTriggerNode';
import { ScheduleTriggerNode } from './ScheduleTriggerNode';
import { MessageTextNode } from './MessageTextNode';
import { MessageMenuNode } from './MessageMenuNode';
import { LogicConditionNode } from './LogicConditionNode';
import { LogicSwitchNode } from './LogicSwitchNode';
import { LogicWorkingHoursNode } from './LogicWorkingHoursNode';
import { AiIntentNode } from './AiIntentNode';
import { AiFaqNode } from './AiFaqNode';
import { AiSentimentNode } from './AiSentimentNode';
import { ActionDelayNode } from './ActionDelayNode';
import { ActionHandoffNode } from './ActionHandoffNode';
import { ActionAssignGroupNode } from './ActionAssignGroupNode';
import { ActionApiCallNode } from './ActionApiCallNode';
import { UtilitySetVariableNode } from './UtilitySetVariableNode';
import { UtilityNoteNode } from './UtilityNoteNode';
import { CallFlowNode } from './CallFlowNode';
import { ActionEcommerceNode } from './ActionEcommerceNode';

export const nodeTypes: NodeTypes = {
  trigger_start: TriggerStartNode,
  webhook_trigger: WebhookTriggerNode,
  outbound_trigger: OutboundTriggerNode,
  schedule_trigger: ScheduleTriggerNode,
  message_text: MessageTextNode,
  message_menu: MessageMenuNode,
  logic_condition: LogicConditionNode,
  logic_switch: LogicSwitchNode,
  logic_working_hours: LogicWorkingHoursNode,
  ai_intent: AiIntentNode,
  ai_faq: AiFaqNode,
  ai_sentiment: AiSentimentNode,
  action_delay: ActionDelayNode,
  action_handoff: ActionHandoffNode,
  action_assign_group: ActionAssignGroupNode,
  action_api_call: ActionApiCallNode,
  utility_set_variable: UtilitySetVariableNode,
  utility_note: UtilityNoteNode,
  action_call_flow: CallFlowNode,
  action_ecommerce: ActionEcommerceNode,
};
