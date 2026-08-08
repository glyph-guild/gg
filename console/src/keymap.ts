/**
 * Pure keymap: (input, key, context) -> Command | null.
 * No Ink imports — KeyInfo is a structural subset of Ink's Key so this stays
 * testable without a terminal. Extend Command and the bindings together.
 */
export type Command = { kind: 'quit' } | { kind: 'toggle-help' };

export interface KeyInfo {
  readonly ctrl: boolean;
  readonly escape: boolean;
}

export interface KeymapContext {
  readonly mode: 'normal' | 'help';
}

export function resolveKey(input: string, key: KeyInfo, context: KeymapContext): Command | null {
  if (key.ctrl && input === 'c') {
    return { kind: 'quit' };
  }
  if (context.mode === 'help') {
    if (key.escape || input === 'q' || input === '?') {
      return { kind: 'toggle-help' };
    }
    return null;
  }
  if (input === 'q') {
    return { kind: 'quit' };
  }
  if (input === '?') {
    return { kind: 'toggle-help' };
  }
  return null;
}
