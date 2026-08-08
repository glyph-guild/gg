import { describe, expect, it } from 'vitest';
import { resolveKey } from './keymap.js';

// Committed failing first, then fixed — proves the harness actually runs
// and CI actually gates on it.
describe('harness proof', () => {
  it('q quits from normal mode', () => {
    expect(resolveKey('q', { ctrl: false, escape: false }, { mode: 'normal' })).toBeNull();
  });
});
