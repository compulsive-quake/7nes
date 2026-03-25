---
name: done
description: Update CHANGELOG.md with changes since the last commit, increment the build number in ModInfo.xml, commit, and push.
disable-model-invocation: true
user-invocable: true
allowed-tools: Bash, Read, Edit, Write, Glob, Grep
argument-hint: [optional commit message override]
---

# Done — Changelog + Version Bump + Commit + Push

## Step 1: Gather changes since the last commit

Run these commands to collect context:

```bash
cd "C:\Users\Richard\WebstormProjects\7nes"
git diff HEAD --stat
git diff HEAD --name-only
git diff HEAD
```

Also run `git status` to catch any untracked files.

Analyze all staged/unstaged changes and untracked files. Summarize what was changed and why, grouping by category (e.g., Emulator Core, Integration, Config, UI, Build, etc.).

## Step 2: Update CHANGELOG.md

Read `CHANGELOG.md` from the project root. If it doesn't exist, create it with a header:

```markdown
# Changelog
```

Read `ModInfo.xml` to get the current version. The next version will have the build number (third component) incremented by 1 (e.g., `1.0.3` → `1.0.4`).

Prepend a new entry at the top of the changelog (after the `# Changelog` header) in this format:

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Category Name
- Description of change

### Another Category
- Another change
```

Use today's date. Use the NEW (incremented) version number. Keep entries concise — one line per logical change. Group by these categories as applicable: **Added**, **Changed**, **Fixed**, **Removed**.

## Step 3: Increment build number in ModInfo.xml

Read `ModInfo.xml`, find the `<Version value="X.Y.Z" />` line, and increment the last number (Z) by 1. Edit the file in place.

## Step 4: Commit all changes

Stage ALL modified and untracked files (but not files matching .gitignore):

```bash
git add -A
```

Commit with a message. If the user provided an argument, use that as the commit message. Otherwise, generate a concise commit message summarizing the changes. Always append the co-author line:

```
Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

Use a heredoc to format the commit message.

## Step 5: Push

Push to the remote:

```bash
git push
```

If the current branch has no upstream, use `git push -u origin <branch>`.

## Step 6: Report

Print a summary:
- The new version number
- Number of files changed
- The changelog entry that was added
- Confirm the push succeeded
