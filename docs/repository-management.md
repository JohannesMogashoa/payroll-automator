# Repository management

The repository labels are configured for the current delivery workflow:

- `phase-0-foundation`
- `phase-5-automation`
- `phase-6-web`
- `priority-high`
- `security-privacy`
- `needs-decision`

The GitHub Project board requires the GitHub CLI token to include the separate
`project` scope. The repository token currently has `repo` access but not that
scope, so board creation cannot be completed non-interactively from this
workspace.

After authenticating interactively, create the board with:

```bash
gh auth refresh -h github.com -s project
gh project create --owner JohannesMogashoa --title "Payroll Automator delivery" --format board
```

Add the repository as the board's item source, then create views for **Backlog**,
**In progress** and **Done**. Use the phase labels above as the grouping/filter
dimension. Do not put payroll files, employee data or deployment secrets into
issues or project fields.
