---
name: line-worker
description: Carries out one bounded piece of a line of development from docs/directions/, in its own git worktree, and reports back without committing. Use only when explicitly asked to fan a line's work out to workers. The worktree the harness gives it is removed if it stops with nothing changed, which is its state at a plan checkpoint, so a worker asked to stop at one writes something first (see the body).
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

The harness gives you a worktree of your own and keeps you in it, and removes it if you stop with
nothing changed in it. If you are asked to stop at a checkpoint before writing code, first write
your plan to `.line-worker/plan.md` in the worktree, so that the worktree, and anything you probed
there, survives until you are resumed; the coordinator does not collect that folder. Give the
worktree's path as the Worktree in every report, checkpoints included.
