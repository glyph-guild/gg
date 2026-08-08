#!/usr/bin/env bash
# PostToolUse [Write|Edit]: keep edited C# formatted.
file_path=$(jq -r '.tool_input.file_path // empty')
case "$file_path" in
  *.cs)
    cd "$CLAUDE_PROJECT_DIR" || exit 0
    dotnet format --no-restore --include "${file_path#"$CLAUDE_PROJECT_DIR/"}" >/dev/null 2>&1 || true
    ;;
esac
exit 0
