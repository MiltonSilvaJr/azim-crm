# custom/ — repo-local overrides (no fork)

Override any template-provided file by recreating its **relative path** under this directory
(pattern adopted from BMAD v6). Examples:

- `custom/rules/conventions/naming.md` overrides `.forge/rules/conventions/naming.md`
- `custom/templates/spec/requirements.md` overrides the spec template

Resolution order: `custom/` first, then the installed template file. Commands, agents and
`sync-adapters` resolve through this order; adapters are regenerated when an override changes.

Rules:
1. Never edit installed template files in place — override here instead, so template upgrades
   stay mergeable.
2. `forge doctor` flags **orphan overrides** (override whose template counterpart no longer
   exists) as drift to clean up.
3. Overrides are committed — they are team policy for this repo.
