# IntentBlazor Governance

Tory Bertram ([Kita72](https://github.com/Kita72)) is the initial project owner, lead architect, maintainer, and release owner. AIMIE is Tory's AI coding partner; it is not an independent maintainer or authority.

## Decisions

Tory plans and directs work in Codex. GitHub issues and Discussions are optional coordination surfaces, not a required project-management workflow. Changes to the output schema, authority boundaries, storage contracts, mandatory dependencies, or public APIs need a short architecture proposal and a recorded decision.

The project owner resolves architectural or release decisions with a written rationale. Contributions are reviewed for correctness, usability, scope, compatibility, and maintainability.

## Review and releases

Maintainer work is verified locally in Codex, committed and pushed directly to `main`. Keep only `main` upstream; no maintainer pull request or automatic CI is required. Record checks actually completed rather than implying independent review. Requested external contributions use contributor-owned forks and local maintainer verification. Dependabot, scheduled dependency updates and GitHub Actions stay disabled unless Tory explicitly changes this decision; see [decision 0014](docs/architecture/0014-main-only-repository.md).

Only maintainers designated by the project owner may publish packages, change repository access, or deploy public services. Bootstrap and repository administration are distinct from routine contribution.

## Maintainers and conduct

Contributors can become maintainers through sustained contributions, review, and stewardship of an area. The project owner proposes the role and access scope publicly and confirms the contributor agrees before granting access.

Contact the owner through [the GitHub profile](https://github.com/Kita72) to arrange private conduct reporting. Do not put sensitive details in a public discussion.

## Compatibility

Preview APIs and document schemas may change. Record breaking changes and migration guidance. Publish support commitments with releases rather than promising a cadence before maintainer capacity is known.
