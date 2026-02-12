import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

let nodeIdCounter = 0;

export function generateNodeId(type: string): string {
  nodeIdCounter++;
  return `${type}_${Date.now()}_${nodeIdCounter}`;
}

export function generateEdgeId(source: string, target: string, sourceHandle?: string): string {
  const handlePart = sourceHandle ? `_${sourceHandle}` : '';
  return `e_${source}${handlePart}_${target}`;
}
