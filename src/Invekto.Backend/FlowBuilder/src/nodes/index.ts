import type { NodeTypes } from '@xyflow/react';
import { TriggerStartNode } from './TriggerStartNode';
import { MessageTextNode } from './MessageTextNode';
import { MessageMenuNode } from './MessageMenuNode';
import { LogicConditionNode } from './LogicConditionNode';
import { LogicSwitchNode } from './LogicSwitchNode';
import { ActionDelayNode } from './ActionDelayNode';
import { ActionHandoffNode } from './ActionHandoffNode';
import { UtilitySetVariableNode } from './UtilitySetVariableNode';
import { UtilityNoteNode } from './UtilityNoteNode';

export const nodeTypes: NodeTypes = {
  trigger_start: TriggerStartNode,
  message_text: MessageTextNode,
  message_menu: MessageMenuNode,
  logic_condition: LogicConditionNode,
  logic_switch: LogicSwitchNode,
  action_delay: ActionDelayNode,
  action_handoff: ActionHandoffNode,
  utility_set_variable: UtilitySetVariableNode,
  utility_note: UtilityNoteNode,
};
