#!/usr/bin/env bash
# PreToolUse hook for Bash, used by the line-worker subagent.
#
# A worker leaves its changes uncommitted in its worktree for the coordinator to
# collect, so it may read git but not write to it. Every git subcommand found in
# the command is checked against a read-only allowlist; anything else is refused
# with exit code 2, which blocks the call and shows the message to the agent.
#
# Requires: jq

set -euo pipefail

COMMAND="$(jq -r '.tool_input.command // empty')"
[[ -z "$COMMAND" ]] && exit 0

READ_ONLY=" status diff log show blame ls-files ls-tree grep rev-parse merge-base describe shortlog cat-file diff-tree name-rev for-each-ref "

# "git", any global options (including -C <path> and -c <key=value>), then the subcommand.
PATTERN='(^|[^[:alnum:]_.-])git([[:space:]]+(-C|-c|--git-dir|--work-tree)[[:space:]]+[^[:space:]]+|[[:space:]]+-[^[:space:]]+)*[[:space:]]+[a-z][a-z-]*'

while IFS= read -r match; do
  subcommand="${match##*[[:space:]]}"
  if [[ "$READ_ONLY" != *" $subcommand "* ]]; then
    echo "git $subcommand is not available to a line worker: leave your changes uncommitted in the worktree for the coordinator to collect. Read-only git commands are allowed." >&2
    exit 2
  fi
done < <(grep -oE "$PATTERN" <<< "$COMMAND" || true)

exit 0
