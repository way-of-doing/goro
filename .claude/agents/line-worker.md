---
name: line-worker
description: Carries out one bounded piece of a line of development from docs/directions/, in its own git worktree, and reports back without committing. Use only when explicitly asked to fan a line's work out to workers.
isolation: worktree
model: inherit
disallowedTools: Agent
skills:
  - line-worker
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "bash \"${CLAUDE_PROJECT_DIR}/.claude/hooks/block-git-writes.sh\""
---
