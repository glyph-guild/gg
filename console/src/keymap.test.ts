import { describe, expect, it } from 'vitest';
import { resolveKey, type KeyInfo } from './keymap.js';
import { initialState, reduce } from './state/appState.js';

const plain: KeyInfo = { ctrl: false, escape: false };

describe('resolveKey', () => {
  it('quits on q in normal mode', () => {
    expect(resolveKey('q', plain, { mode: 'normal' })).toEqual({ kind: 'quit' });
  });

  it('quits on ctrl+c in any mode', () => {
    expect(resolveKey('c', { ...plain, ctrl: true }, { mode: 'help' })).toEqual({ kind: 'quit' });
  });

  it('toggles help on ?', () => {
    expect(resolveKey('?', plain, { mode: 'normal' })).toEqual({ kind: 'toggle-help' });
  });

  it('closes help on escape instead of quitting', () => {
    expect(resolveKey('', { ...plain, escape: true }, { mode: 'help' })).toEqual({
      kind: 'toggle-help',
    });
  });

  it('returns null for unbound keys', () => {
    expect(resolveKey('x', plain, { mode: 'normal' })).toBeNull();
  });
});

describe('appState', () => {
  it('round-trips through JSON (fully serializable)', () => {
    const state = reduce(initialState, { kind: 'toggle-help' });
    expect(JSON.parse(JSON.stringify(state))).toEqual(state);
  });

  it('toggle-help toggles back to normal', () => {
    const once = reduce(initialState, { kind: 'toggle-help' });
    expect(reduce(once, { kind: 'toggle-help' })).toEqual(initialState);
  });
});
