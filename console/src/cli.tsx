#!/usr/bin/env node
import { createRequire } from 'node:module';
import React from 'react';
import { render } from 'ink';
import { App } from './components/App.js';

const { version } = createRequire(import.meta.url)('../package.json') as { version: string };

const args = process.argv.slice(2);
if (args.includes('--version') || args.includes('-v')) {
  console.log(`gg ${version}`);
  process.exit(0);
}

render(<App />);
