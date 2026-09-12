#!/usr/bin/env bash
# Claude Code SessionEnd hook.
#
# Claude Code sends JSON on stdin describing the event, including:
#   - session_id       the session's UUID
#   - transcript_path  path to the raw JSONL transcript Claude Code already
#                       keeps for this session (used for /resume etc.)
#
# This script converts that transcript into a readable Markdown file under
# docs/sessions/, so it gets committed alongside the code/doc changes from
# the same session.
#
# Requires: jq

set -euo pipefail

INPUT="$(cat)"
TRANSCRIPT_PATH="$(echo "$INPUT" | jq -r '.transcript_path // empty')"
SESSION_ID="$(echo "$INPUT" | jq -r '.session_id // "unknown"')"

# Nothing to export — don't block session end over it.
if [[ -z "$TRANSCRIPT_PATH" || ! -f "$TRANSCRIPT_PATH" ]]; then
  exit 0
fi

OUT_DIR="docs/sessions"
mkdir -p "$OUT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
OUT_FILE="$OUT_DIR/${STAMP}-${SESSION_ID:0:8}.md"

{
  echo "# Session ${SESSION_ID}"
  echo "_Exported $(date -Iseconds)_"
  echo

  # Pull out plain user text and assistant text blocks, in order, and
  # render them as a simple back-and-forth transcript. Tool calls/results
  # are skipped to keep this readable; the full JSONL is still at
  # $TRANSCRIPT_PATH if you ever need it.
  jq -r '
    select(.type == "user" or .type == "assistant") |
    if .type == "user" then
      (.message.content) as $c |
      if ($c | type) == "string" then "## User\n\n" + $c + "\n" else empty end
    else
      ((.message.content // []) | map(select(.type == "text") | .text) | join("\n")) as $t |
      if ($t | length) > 0 then "## Claude\n\n" + $t + "\n" else empty end
    end
  ' "$TRANSCRIPT_PATH"
} > "$OUT_FILE"

echo "Session exported to $OUT_FILE" >&2
exit 0
