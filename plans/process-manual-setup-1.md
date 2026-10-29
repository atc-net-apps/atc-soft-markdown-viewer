---
goal: One-time manual setup for atc-soft-markdown-viewer (GitHub org atc-net-apps, release-please token, winget, free code signing)
version: 1.0
date_created: 2026-10-23
last_updated: 2026-10-23
owner: Per Kops (atc-net-apps)
status: 'On Hold'
tags: [process, setup, github, winget, code-signing]
---

# Introduction

![Status: On Hold](https://img.shields.io/badge/status-On%20Hold-orange)

The owner does these steps later, when the code is pushed to GitHub. They are not needed for local development.

This plan covers setup steps an agent cannot do. They need a human with org-owner rights, a browser, or an application to a third party. [feature-markdown-viewer-1.md](feature-markdown-viewer-1.md) marks the tasks that depend on these steps with **[MANUAL: Mxx]**.

The GitHub org `atc-net-apps` is new and has nothing configured yet. The release pattern is copied from `atc-net` repositories such as `atc-net/atc-coding-rules-updater`, which already rely on a `RELEASE_PLEASE_PAT` secret. That secret has to be created again in the new org.

## For Future Agents

As work proceeds: mark each task completed with its date; set the front matter `status` and each phase's `Status`; run the phase's **Verification** and record the result before starting the next phase; write the **Phase Summary** when a phase completes. When all phases are done, fill in **Final Recap** and **Deployment Plan**.

An agent can check most verifications with `gh`. An agent never creates tokens or completes identity checks.

## 1. Requirements & Constraints

- **REQ-001**: `RELEASE_PLEASE_PAT` must exist before `.github/workflows/release-please.yml` is merged (main plan TASK-006).
- **REQ-002**: Branch protection requires the `dotnet-build` and `dotnet-test` checks once the repository is on GitHub.
- **REQ-003**: The first winget submission happens after GitHub Release v0.1.0 exists with its installers (main plan TASK-096).
- **REQ-004**: Releases are unsigned until SignPath approves the project (Phase 3). The pipeline skips signing while `SIGNPATH_ORGANIZATION_ID` is unset. Paid options are out of scope: there is no business entity and no budget.
- **SEC-001**: Tokens are stored only as GitHub Actions secrets, never in files. Each token has the narrowest scope that works and an expiry date with a calendar reminder.
- **CON-001**: Every step is done by a human with owner rights on `atc-net-apps`.

## 2. Implementation Steps

### Implementation Phase 1: GitHub org and repository

Status: Not started

- GOAL-001: The `atc-net-apps` org and the `atc-soft-markdown-viewer` repository can run release-please and enforce PR checks.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-M01 | In org **Settings → Personal access tokens → Settings**, allow fine-grained personal access tokens. Choose whether they need approval; if they do, approve your own token in M03. | | |
| TASK-M02 | In org **Settings → Actions → General**, set "Allow all actions and reusable workflows". Alternatively, allow only these: `actions/*`, `googleapis/release-please-action`, `vedantmgoyal9/winget-releaser`, and later `signpath/*`. Set workflow permissions to "Read repository contents". | | |
| TASK-M03 | Create a fine-grained PAT named `release-please (atc-net-apps)`:<br>• Resource owner: `atc-net-apps`.<br>• Repository access: "All repositories", or only `atc-soft-markdown-viewer`.<br>• Repository permissions: Contents read and write, Pull requests read and write, Issues read and write (release-please manages labels such as `autorelease: pending`), Metadata read.<br>• Expiration: 1 year.<br><br>Store it as the org secret **`RELEASE_PLEASE_PAT`** (org **Settings → Secrets and variables → Actions**), available to the repository. Add a calendar reminder 2 weeks before expiry. | | |
| TASK-M04 | In repository **Settings → General → Pull Requests**, keep whichever merge methods you like (no restriction), and turn on "Automatically delete head branches". Add a ruleset (or branch protection) on `main`: require a pull request, require the status checks `dotnet-build` and `dotnet-test` (from `build.yml`), and block force pushes. The checks only appear in the picker after they have run once, so finish this step after the first PR from main plan Phase 1. | | |

#### Verification

- **VER-001**: `gh secret list --org atc-net-apps` lists `RELEASE_PLEASE_PAT`.
- **VER-002**: `gh api repos/atc-net-apps/atc-soft-markdown-viewer/rules/branches/main --jq '.[].type'` includes `required_status_checks` and `pull_request`.
- **VER-003**: Merging the first PR containing a `feat:` commit makes release-please open "chore: release version 0.1.0", and the build checks run on that release PR.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 2: winget

Status: Not started

- GOAL-002: `atc-net.MarkdownViewer` exists in winget, and later versions are submitted automatically.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-M10 | Choose the GitHub account that will open winget-pkgs PRs: your own account or a bot account. From that account, fork `microsoft/winget-pkgs`. | | |
| TASK-M11 | From the same account, create a **classic** PAT with the `public_repo` scope (winget-releaser documents a classic token) and a 1-year expiry. Store it as the repository secret **`WINGET_TOKEN`** on `atc-net-apps/atc-soft-markdown-viewer`. Add a calendar reminder. | | |
| TASK-M12 | After GitHub Release v0.1.0 exists, run `winget install wingetcreate`, then `wingetcreate new https://github.com/atc-net-apps/atc-soft-markdown-viewer/releases/download/v0.1.0/atc-mdv-0.1.0-win-x64-setup.exe https://github.com/atc-net-apps/atc-soft-markdown-viewer/releases/download/v0.1.0/atc-mdv-0.1.0-win-arm64-setup.exe`. Enter:<br>• PackageIdentifier `atc-net.MarkdownViewer`, Publisher `atc-net`, PackageName `ATC Markdown Viewer`, License `MIT`;<br>• ShortDescription "Fast markdown viewer and editor for Windows";<br>• InstallerType `inno` and Scope `user` (wingetcreate usually detects both);<br>• Commands `atc-mdv`, FileExtensions `md` and `markdown`.<br><br>Submit with `--submit` and your token. On your first winget-pkgs PR, sign the Microsoft CLA when the bot asks. | | |
| TASK-M13 | Wait for the winget-pkgs validation pipeline and moderator review, which usually take one to several days. Fix any validation comments. After the PR merges, set the repository variable **`WINGET_ENABLED`** to `true` (**Settings → Secrets and variables → Actions → Variables**). | | |

#### Verification

- **VER-004**: `winget search atc-net.MarkdownViewer` returns the package.
- **VER-005**: `gh variable list -R atc-net-apps/atc-soft-markdown-viewer` shows `WINGET_ENABLED  true`.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 3: Free code signing (optional)

Status: Not started

- GOAL-003: Releases are signed for free through SignPath Foundation. If that is not possible, the project stays unsigned, which is a supported state.

**Why sign at all.** Windows SmartScreen shows "Windows protected your PC" for unsigned installers downloaded in a browser, and users have to click "More info" and then "Run anyway". Some company machines block unsigned apps outright. Signing proves who published the file. It does not remove the warning straight away, because Microsoft builds a publisher's reputation over weeks of clean installs.

**Why only SignPath.** Paid options are ruled out:
- Azure Artifact Signing costs about USD 10 per month and needs a business with 3 or more years of tax history; individual developers qualify only in the US and Canada.
- CA certificates cost USD 300–500 per year.

SignPath Foundation signs open-source projects for free, with a certificate issued to SignPath Foundation itself. The project must be open source (MIT qualifies), built in CI from the public repository, and approved by SignPath. Check the current terms at signpath.org when applying, because they may change.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-M20 | After v0.1.0 is public and the README describes the project, apply at https://signpath.org/ (the open-source program) with the repository URL, license, build workflow and the files to sign (`atc-mdv.exe`, `atc-mdv-*-setup.exe`). Record the application date in `docs/adr/0002-code-signing.md`. | | |
| TASK-M21 | If approved: follow SignPath's GitHub Actions onboarding. Set the repository variables and secrets they issue (organization ID, project slug, signing policy slug, API token), and record their names in `docs/adr/0002-code-signing.md`. Then main plan TASK-093 can start. | | |
| TASK-M22 | If not approved, or not applied for: record "unsigned" and the reason in `docs/adr/0002-code-signing.md`. Nothing else changes; releases stay unsigned. | | |

#### Verification

- **VER-006**: `docs/adr/0002-code-signing.md` exists and states either "SignPath" or "unsigned".
- **VER-007**: SignPath only. After the next release, `Get-AuthenticodeSignature .\atc-mdv-<ver>-win-x64-setup.exe` reports `Status: Valid`.

#### Phase Summary

_(Write when the phase completes.)_

## 3. Alternatives

- **ALT-001**: A GitHub App token (`actions/create-github-app-token`) instead of `RELEASE_PLEASE_PAT`. It avoids yearly expiry and personal-account coupling. Not chosen for now, to match the existing atc-net pattern; it is worth adopting org-wide later.
- **ALT-002**: Scoop. Dropped by the owner; winget is enough.
- **ALT-003**: Azure Artifact Signing and paid CA certificates. Rejected: there is no business entity, Artifact Signing does not accept individuals outside the US and Canada, and the owner does not want to pay.
- **ALT-004**: Microsoft Store distribution (the Store signs MSIX packages). It could be revisited later as a free extra channel; check the current developer-account terms first.

## 4. Dependencies

- **DEP-001**: Org-owner rights on `atc-net-apps`.
- **DEP-002**: For signing: approval from SignPath Foundation.

## 5. Files

- **FILE-001**: `docs/adr/0002-code-signing.md`: the signing status (SignPath or unsigned).

## 6. Testing

- **TEST-001**: The verifications in each phase. These steps involve no code.

## 7. Risks & Assumptions

- **RISK-001**: PATs expire. An expired `RELEASE_PLEASE_PAT` silently stops release PRs, and an expired `WINGET_TOKEN` fails the winget job. Mitigations: calendar reminders, and ALT-001 later.
- **RISK-002**: winget moderators can reject or delay the first submission. Mitigation: users can still download from GitHub Releases in the meantime.
- **ASSUMPTION-001**: The winget identifier `atc-net.MarkdownViewer` uses the publisher segment `atc-net` even though the GitHub org is `atc-net-apps`. The owner confirmed this on 2026-10-23.

## 8. Related Specifications / Further Reading

- [Main plan](feature-markdown-viewer-1.md)
- [SmartScreen reputation for Windows app developers](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation)
- [wingetcreate](https://github.com/microsoft/winget-create) and [winget-pkgs contributing guide](https://github.com/microsoft/winget-pkgs/blob/master/CONTRIBUTING.md)
- [winget-releaser](https://github.com/vedantmgoyal9/winget-releaser)
- [SignPath Foundation](https://signpath.org/)

## 9. Final Recap

_(Write when all phases complete.)_

## 10. Deployment Plan

_(Write when all phases complete.)_
