import type { Command } from '../keymap.js';

/**
 * App state is plain data: it must round-trip through JSON unchanged so a
 * session can be serialized, shipped, and replayed. No functions, no class
 * instances, no Dates.
 */
export interface AppState {
  readonly mode: 'normal' | 'help';
}

export const initialState: AppState = { mode: 'normal' };

export function reduce(state: AppState, command: Command): AppState {
  switch (command.kind) {
    case 'toggle-help':
      return { ...state, mode: state.mode === 'help' ? 'normal' : 'help' };
    case 'quit':
      // Quitting tears down the app; it is handled by the shell, not state.
      return state;
  }
}
