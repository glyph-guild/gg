import React, { useReducer } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import { resolveKey } from '../keymap.js';
import { initialState, reduce } from '../state/appState.js';

export function App(): React.JSX.Element {
  const { exit } = useApp();
  const [state, dispatch] = useReducer(reduce, initialState);

  useInput((input, key) => {
    const command = resolveKey(input, key, { mode: state.mode });
    if (command === null) {
      return;
    }
    if (command.kind === 'quit') {
      exit();
      return;
    }
    dispatch(command);
  });

  return (
    <Box flexDirection="column">
      <Text bold>Good Grief</Text>
      {state.mode === 'help' ? (
        <Text>q quit · ? close help</Text>
      ) : (
        <Text dimColor>console stub — ? for help, q to quit</Text>
      )}
    </Box>
  );
}
