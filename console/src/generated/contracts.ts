// GENERATED from Gg.Contracts — do not edit.
// Regenerate: dotnet run --project tools/Gg.ContractsGen
import { z } from 'zod';

export const protocolHelloSchema = z.object({
  protocolVersion: z.number().int(),
  component: z.string(),
  componentVersion: z.string(),
});
export type ProtocolHello = z.infer<typeof protocolHelloSchema>;
