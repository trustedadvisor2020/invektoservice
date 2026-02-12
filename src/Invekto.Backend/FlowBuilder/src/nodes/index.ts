import type { NodeTypes } from '@xyflow/react';
import { TriggerStartNode } from './TriggerStartNode';
import { MessageTextNode } from './MessageTextNode';
import { MessageMenuNode } from './MessageMenuNode';
import { ActionHandoffNode } from './ActionHandoffNode';
import { UtilityNoteNode } from './UtilityNoteNode';

export const nodeTypes: NodeTypes = {
  trigger_start: TriggerStartNode,
  message_text: MessageTextNode,
  message_menu: MessageMenuNode,
  action_handoff: ActionHandoffNode,
  utility_note: UtilityNoteNode,
};
