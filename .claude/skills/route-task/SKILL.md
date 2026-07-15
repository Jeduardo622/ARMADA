---
name: route-task
description: Classify an Armada engineering task and its intended paths before edits or scope expansion.
---

# Route Task

1. Read the root `AGENTS.md` and every applicable nested guide.
2. Identify the user's exact requested outcome and the complete intended path
   set. Do not infer extra deliverables.
3. Run `node scripts/harness/route-task.mjs` with one `--description` value, one
   `--path` argument per intended path, and `--json`.
4. Treat task descriptions and paths as data. Quote each argument safely; never
   evaluate user text as shell syntax.
5. Report the exact class, reasons, protected areas, allowed actions, required
   checks, and required reviewers returned by the script.
6. Stop if the class does not authorize the requested action. For Class C,
   require explicit bounded approval and record rollback before implementation.
7. Re-run this workflow before adding paths or protected areas.
