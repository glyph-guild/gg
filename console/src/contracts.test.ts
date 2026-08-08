import { describe, expect, it } from 'vitest';
import { protocolHelloSchema } from './generated/index.js';

describe('generated contracts', () => {
  it('accepts a valid ProtocolHello', () => {
    expect(
      protocolHelloSchema.parse({
        protocolVersion: 1,
        component: 'console',
        componentVersion: '0.1.0',
      }),
    ).toMatchObject({ component: 'console' });
  });

  it('rejects a non-integer protocol version', () => {
    expect(() =>
      protocolHelloSchema.parse({
        protocolVersion: 1.5,
        component: 'console',
        componentVersion: '0.1.0',
      }),
    ).toThrow();
  });
});
