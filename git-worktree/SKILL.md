---
name: git-worktree
description: >
  Safely guide Git worktree workflows: creating a separate checkout for a branch or task,
  listing active worktrees, moving, locking, unlocking, removing, repairing, or pruning
  worktrees, and resolving errors caused by a branch already being checked out elsewhere.
  Use this skill whenever a user mentions `git worktree`, parallel Git checkouts, working on
  multiple branches at once, a worktree path that was moved or deleted manually, or wants to
  clean up a stale worktree. Before proposing a state-changing command, inspect the repository
  and affected worktrees, preserve Git's safeguards, and make destructive consequences explicit.
license: Proprietary. LICENSE has complete terms.
---

# Git Worktree Skill

Help users manage multiple working directories from one Git repository without accidentally
sharing a branch, deleting uncommitted changes, or removing worktree metadata that Git needs.
Git allows one non-detached branch to be checked out by only one worktree at a time; verify this
constraint before creating or switching a worktree.

## Source of truth

Use the current local Git help and the official documentation when command behavior or version
support matters:

- `git worktree -h`
- https://git-scm.com/docs/git-worktree

Do not state that a Git option is available until it has been checked against the user's Git
version when compatibility is relevant.

## Workflow

### 1. Identify the intent and affected paths

Determine whether the user wants to create, inspect, move, lock, unlock, remove, repair, or
prune a worktree. Identify the repository, target path, branch, and starting commit. Ask one
focused clarification question when any of these would change the command or risk profile.

Before a state-changing command, inspect:

```bash
git rev-parse --show-toplevel
git worktree list --porcelain
git branch --show-current
git status --short
```

For an existing worktree, inspect it explicitly:

```bash
git -C <worktree-path> status --short
git -C <worktree-path> branch --show-current
```

Use `git worktree list --porcelain` when a script or reliable parsing is needed. It is stable
across Git versions and configuration; combine it with `-z` only when code must safely parse
unusual path names.

### 2. Choose the safest command

Prefer explicit branches and paths. Do not rely on Git inferring a branch name from a directory
name when a user needs a predictable checkout.

| Goal | Safe starting command |
| --- | --- |
| New worktree with a new task branch | `git worktree add -b <branch> <path> [<start-point>]` |
| Worktree for an existing branch that is not checked out elsewhere | `git worktree add <path> <branch>` |
| Temporary inspection at a commit without a branch | `git worktree add --detach <path> <commit-ish>` |
| See all registrations and their state | `git worktree list --verbose` |
| Protect a portable or long-lived worktree | `git worktree lock --reason "<reason>" <path>` |
| Allow an intentionally locked worktree to be managed | `git worktree unlock <path>` |
| Relocate a registered worktree | `git worktree move <worktree> <new-path>` |
| Reconnect metadata after moving/deleting paths manually | `git worktree repair [<path>...]` |
| Preview stale-registration cleanup | `git worktree prune --dry-run --verbose` |
| Remove a clean worktree | `git worktree remove <path>` |

Use a relative sibling path such as `../project-feature` only when the user has confirmed the
layout. Otherwise show the exact absolute or repository-relative path being used.

### 3. Respect safeguards

- Never suggest `-B` for a new worktree unless the user explicitly intends to reset an existing
  branch; it can discard branch history by resetting it to the chosen start point.
- Do not add `--force` merely to bypass an error. Explain which protection Git reported, inspect
  the affected branch or path, and offer a non-destructive alternative first.
- If a branch is already checked out, list worktrees and use a different branch, remove the old
  worktree only after checking it is clean, or make a detached checkout when that serves the
  user's purpose. Do not force the same branch into two normal worktrees.
- Before `remove`, check `git -C <path> status --short`. An empty result supports ordinary
  removal; non-empty output means changes need committing, moving, stashing, or explicit
  destructive confirmation.
- `git worktree remove --force` discards an unclean worktree. A locked worktree needs force
  twice. Only show either form after the user has explicitly accepted that loss or override.
- Start stale cleanup with `git worktree prune --dry-run --verbose`; run `prune --verbose` only
  after reviewing the preview. Use `repair` when the on-disk worktree still exists but its
  registration links are out of date.
- Treat paths containing spaces as one shell argument. Quote them in shell examples.

### 4. Verify the result

After every mutation, show a concise verification sequence:

```bash
git worktree list --verbose
git -C <worktree-path> status --short
git -C <worktree-path> branch --show-current
```

For a newly created worktree, also confirm the branch relationship when relevant:

```bash
git -C <worktree-path> status --branch --short
```

## Common scenarios

### Start a task without leaving the current branch

```bash
git fetch origin
git worktree add -b feature/<topic> ../<repository>-<topic> origin/main
```

Explain the chosen start point and replace `origin/main` with the repository's actual integration
branch when it differs. If the repository does not need a fetch, omit it rather than adding
network activity by default.

### Work on an existing local branch

First confirm it is not already checked out:

```bash
git worktree list --verbose
git branch --show-current
```

Then use:

```bash
git worktree add ../<repository>-<branch> <branch>
```

### Remove a completed worktree

```bash
git -C ../<repository>-<topic> status --short
git worktree remove ../<repository>-<topic>
git worktree list --verbose
```

If status is not clean, stop and explain the choices. Do not silently stash or discard work.

### Repair after a manual filesystem move

If the primary repository and moved worktree still exist, run from the primary repository:

```bash
git worktree repair <new-worktree-path>
git worktree list --verbose
```

If a worktree directory was intentionally deleted manually, preview stale entries first:

```bash
git worktree prune --dry-run --verbose
git worktree prune --verbose
```

## Response format

Use this order unless the user asks for only a command:

1. **Situation** — current worktree/branch state and the goal.
2. **Safe command** — the smallest command sequence with placeholders replaced where known.
3. **Before running** — branch, path, dirty-state, or destructive-operation caveats that apply.
4. **Verify** — command(s) that prove the intended worktree is registered and clean.

Keep normal operations concise. When blocked by Git safeguards, describe the actual conflict and
present the safest alternatives rather than treating `--force` as the default solution.
