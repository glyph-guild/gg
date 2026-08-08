#!/usr/bin/env bash
# PreToolUse [Write|Edit]: console/src/generated is generated from Gg.Contracts.
file_path=$(jq -r '.tool_input.file_path // empty')
case "$file_path" in
  *console/src/generated/*)
    cat <<'JSON'
{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"generated from Gg.Contracts — regenerate instead of editing"}}
JSON
    ;;
esac
exit 0
