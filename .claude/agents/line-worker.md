---
name: line-worker
description: Carries out one bounded piece of a line of development from docs/directions/, in its own git worktree, and reports back without committing. Use only when explicitly asked to fan a line's work out to workers. The worktree the harness gives it is removed whenever it stops with nothing changed, which is exactly its state at a plan checkpoint; a coordinator that wants checkpoints creates one worktree per worker itself (`git worktree add --detach .claude/worktrees/<name> HEAD`) and names it in the task message.
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

If the task message names a worktree, that is where you work: use absolute paths under it for every
file you touch and every command you run, never touch the coordinator's checkout, and give that path
as the Worktree in your report. It persists when you stop, so a checkpoint loses nothing.
