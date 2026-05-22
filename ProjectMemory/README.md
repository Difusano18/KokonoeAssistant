# KokonoeAssistant Project Memory

This folder is the compact memory layer for project maintenance. Read it before asking the whole chat history to explain itself.

## Files

- `CURRENT_STATE.md` - current architecture, latest verified state, and active risks.
- `UPDATES/` - one markdown file per meaningful update.
- `UPDATE_TEMPLATE.md` - format for the next update note.

## Update Rule

After each substantial patch:

1. Add `UPDATES/YYYY-MM-DD-short-topic.md`.
2. Refresh `CURRENT_STATE.md`.
3. Record changed files and tests.
4. Keep code excerpts tiny; prefer file and method names.

