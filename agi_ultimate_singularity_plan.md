# AGI Ultimate Singularity Plan

## Memory & Context Integrity

- Obsidian profile backups are maintenance artifacts, not memory. Keep them out of vault search, graph indexing, preflight recall, and conversational context.
- Primary backup location: `O:\App\Obsidian\MyBrain\Yasus\Kokonoe\Profile Backups`.
- Backup retention policy: keep one useful profile backup per day; do not create continuous per-change `Profile-YYYYMMDD-HHMMSS.md` spam.
- Cleanup policy: remove backups older than 7 days and collapse redundant backups created within the same hour, keeping the latest file in that window.
- Performance rule: log Obsidian list/search/preflight timings to `Kokonoe_System.log`; repeated vault reads should use cached file indexes or search results instead of scanning the same backup-heavy tree again.
