# SP1-D06 — Subchat recursion: Branch.ParentBranchId (Lane D, P1-WAVE-03)

| Field | Value |
| --- | --- |
| Repository | `Nexus.Experience` (worktree `C:\Personal\Nexus-Exp-W3-D06`) |
| Branch | `sp1-d06-subchat` |
| Base commit (HEAD at lane start) | `617aae7` (`CHG-20260828-008: Azure SQL Stage 4 - Subproject SQL persistence + HTTP API`) |
| Lane | D — Subchat recursion (SP1-D06) |
| Date | 2026-09-07 |
| Working tree | Clean at lane start; all work left as **uncommitted working-tree changes** |

---

## Scope (exactly, nothing more)

Implement the optional Subchat recursion link on the existing `Branch` aggregate:

- Add an optional, nullable **`Branch.ParentBranchId`** (`BranchId?`), so one `Conversation` has a
  tree of `Branch`es and a branch may point at a parent branch within the same `Conversation`.
- Conversation remains the canonical Chat aggregate; **no new "Subchat aggregate" is created or
  renamed**. Branch remains the canonical branch/sub-chat mechanism.
- Prevent **self-parent** (`ParentBranchId == own id`) at the domain/application layer.
- Prevent **cycles** (a branch cannot be an ancestor of its own proposed parent) when creating /
  reparenting a branch, by walking the persisted parent chain.
- Preserve every existing branch create / read / list / update path and its semantics —
  `ParentBranchId` is additive and defaults to `null` (root branch).

## Decisions locked

1. **Domain-first rule, enforced at both layers.**
   - `Branch` ctor takes an optional trailing `parentBranchId` (default `null`) and rejects
     `parentBranchId == id` (`ArgumentException`).
   - New domain method `Branch.ChangeParent(BranchId?, IReadOnlyCollection<BranchId>)` rejects
     self-parent and rejects a parent whose existing ancestor chain already contains this branch
     id (i.e. reparenting onto one of the branch's own descendants). The ancestor set is supplied
     by the caller so the rule is pure/testable; the **authoritative walk of persistence happens
     in the application handlers**.
2. **Application layer owns the authoritative cycle guard.** `CreateBranchHandler` and
   `UpdateBranchHandler` resolve the prospective parent from the repository, verify it exists and
   belongs to the same `Conversation`, and (for reparent) walk the parent's ancestor chain before
   calling `ChangeParent`. Violations throw `InvalidOperationException` — the same convention the
   existing `UpdateSessionHandler` already uses for illegal state transitions.
3. **Update/reparent is additive and backwards compatible.** `UpdateBranchCommand`/`UpdateBranchRequest`
   gain an optional `ParentBranchId`; `null` means *leave the current parent unchanged*, so existing
   PUT clients behave exactly as before. A side effect of this choice: clearing-to-root is available
   through the domain (`ChangeParent(null, …)`, tested) but is not reachable through HTTP PUT (JSON
   cannot distinguish absent from explicit null without a wrapper). Flagged as a deliberate minimal
   decision / follow-up in Risks.
4. **Read affordances surface the link.** `Get`/`List`/`Create`/`Update` results and their HTTP DTOs
   now carry `ParentBranchId` (`Guid?`), so a client can read the parent of any branch.
5. **Persistence.** EF self-referencing FK on `session.Branch.ParentBranchId` with
   `DeleteBehavior.Restrict` (not Cascade) — deleting a parent branch does not silently take its
   sub-branches, and the existing `Conversation -> Branch` cascade does not gain a second cascade
   path through the self link. A dedicated `IX_Branch_ParentBranchId` index is added for ancestor
   walks / direct-sub-branch listing. EF migration `20260907163653_SubchatBranchParent` added.

## Changed-file manifest

### Tracked files modified (19)

| # | File | Change |
| --- | --- | --- |
| 1 | `src/Nexus.Products.Chat.Domain/Branch/Branch.cs` | Added `ParentBranchId` property, optional ctor param + self-parent guard, `ChangeParent` method with self/cycle guards. |
| 2 | `src/Nexus.Products.Chat.Infrastructure/Sql/Configurations/BranchConfiguration.cs` | Mapped nullable `ParentBranchId` (BranchId converter), self-referencing FK (Restrict), index; comment updates. |
| 3 | `src/Nexus.Products.Chat.Infrastructure/Sql/Migrations/NexusChatDbContextModelSnapshot.cs` | Auto-updated by EF tooling to include `ParentBranchId` + self FK. |
| 4 | `src/Nexus.Products.Chat.Application/Branch/Commands/CreateBranchCommand.cs` | Added optional `ParentBranchId`. |
| 5 | `src/Nexus.Products.Chat.Application/Branch/Commands/CreateBranchHandler.cs` | Validates parent exists + same Conversation; passes parent to ctor. |
| 6 | `src/Nexus.Products.Chat.Application/Branch/Commands/CreateBranchResult.cs` | Added `ParentBranchId`. |
| 7 | `src/Nexus.Products.Chat.Application/Branch/Queries/UpdateBranch/UpdateBranchCommand.cs` | Added optional `ParentBranchId` (null = leave unchanged). |
| 8 | `src/Nexus.Products.Chat.Application/Branch/Queries/UpdateBranch/UpdateBranchHandler.cs` | Reparent support + ancestor-chain walk + cycle guard; returns `ParentBranchId`. |
| 9 | `src/Nexus.Products.Chat.Application/Branch/Queries/UpdateBranch/UpdateBranchResult.cs` | Added `ParentBranchId`. |
| 10 | `src/Nexus.Products.Chat.Application/Branch/Queries/GetBranch/GetBranchHandler.cs` | Maps `ParentBranchId` into result. |
| 11 | `src/Nexus.Products.Chat.Application/Branch/Queries/GetBranch/GetBranchResult.cs` | Added `ParentBranchId`. |
| 12 | `src/Nexus.Products.Chat.Application/Branch/Queries/ListBranches/ListBranchesHandler.cs` | Maps `ParentBranchId` into results. |
| 13 | `src/Nexus.Products.Chat.Application/Branch/Queries/ListBranches/ListBranchResult.cs` | Added `ParentBranchId`. |
| 14 | `src/Nexus.Products.Chat.Api/Endpoints/Branches/BranchEndpoint.cs` | Create/Get/List/Put pass and return `ParentBranchId`. |
| 15 | `src/Nexus.Products.Chat.Api/Endpoints/Branches/CreateBranchRequest.cs` | Added optional `Guid? ParentBranchId`. |
| 16 | `src/Nexus.Products.Chat.Api/Endpoints/Branches/CreateBranchResponse.cs` | Added `Guid? ParentBranchId`. |
| 17 | `src/Nexus.Products.Chat.Api/Endpoints/Branches/GetBranchResponse.cs` | Added `Guid? ParentBranchId`. |
| 18 | `src/Nexus.Products.Chat.Api/Endpoints/Branches/ListBranchResponse.cs` | Added `Guid? ParentBranchId`. |
| 19 | `src/Nexus.Products.Chat.Api/Endpoints/Branches/UpdateBranchRequest.cs` | Added optional `Guid? ParentBranchId`. |

### New files (4 + this report)

| # | File | Purpose |
| --- | --- | --- |
| 1 | `src/Nexus.Products.Chat.Infrastructure/Sql/Migrations/20260907163653_SubchatBranchParent.cs` | EF migration `Up`: add nullable `ParentBranchId` column, `IX_Branch_ParentBranchId`, self FK `FK_Branch_Branch_ParentBranchId` (Restrict); symmetric `Down`. |
| 2 | `src/Nexus.Products.Chat.Infrastructure/Sql/Migrations/20260907163653_SubchatBranchParent.Designer.cs` | EF migration designer/snapshot metadata (tool-generated). |
| 3 | `tests/Nexus.Products.Chat.Tests/Branch/BranchHierarchyTests.cs` | Domain aggregate tests (9). |
| 4 | `tests/Nexus.Products.Chat.Tests/Branch/BranchHierarchyApplicationTests.cs` | Application-handler enforcement tests with an in-memory `IBranchRepository` (12). |
| 5 | `architecture/SP1_D06_SUBCHAT_REPORT.md` | This report. |

### Test coverage added (21 tests)

Domain (`BranchHierarchyTests`):
- root branch default: `ParentBranchId` is `null`;
- sub-branch creation: `ParentBranchId` is set;
- self-parent rejected on create (`ArgumentException`) and on `ChangeParent` (`InvalidOperationException`);
- direct cycle rejected (A→B→A) and deeper cycle rejected (A→B→C→A);
- valid reparent to an unrelated root allowed; `ChangeParent(null)` moves a branch back to root;
- existing behavior preserved (trim, rename, description, status transitions on a root branch).

Application (`BranchHierarchyApplicationTests`):
- create root branch stores `null` parent; create under an existing parent stores the sub-branch;
- create with missing parent throws; create with cross-Conversation parent throws;
- update without `ParentBranchId` leaves the current parent unchanged (existing PUT semantics preserved);
- update reparent-to-self throws; update direct cycle (A→B→A) throws; update deeper cycle (A→B→C→A) throws;
- valid reparent of a root sibling under another root persists;
- update of a missing branch returns `null` (unchanged);
- `Get` and `List` expose the parent link.

## Verification evidence

Baseline (before the lane), full suite via `dotnet test Nexus.Experience.slnx`:
```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19  - Nexus.Products.Chat.Tests.dll
Passed!  - Failed: 0, Passed:  4, Skipped: 0, Total:  4  - Nexus.Products.Chat.Architecture.Tests.dll
```

Final build — `dotnet build Nexus.Experience.slnx` (from worktree root):
```
Build succeeded.
    0 Warning(s)   (incremental; see clean-rebuild note below)
    0 Error(s)
```
Clean rebuild of the Domain project (`dotnet build src/Nexus.Products.Chat.Domain/Nexus.Products.Chat.Domain.csproj`
after `rm -rf bin obj`) reports exactly **one pre-existing warning** — `ConversationMessage.cs(15,13):
warning CS8618` — which is present at the baseline commit and is **not** introduced by this lane:
```
Build succeeded.
    1 Warning(s)
    0 Error(s)
```

Final test run — `dotnet test Nexus.Experience.slnx` (from worktree root):
```
Passed!  - Failed: 0, Passed: 40, Skipped: 0, Total: 40  - Nexus.Products.Chat.Tests.dll
Passed!  - Failed: 0, Passed:  4, Skipped: 0, Total:  4  - Nexus.Products.Chat.Architecture.Tests.dll
```
Exit code 0. **Full suite green: 44 passed, 0 failed, 0 skipped** (21 new SP1-D06 tests added; 19 pre-existing unit + 4 architecture tests still pass).

Migration tooling — `dotnet ef migrations add SubchatBranchParent
--project src/Nexus.Products.Chat.Infrastructure/Nexus.Products.Chat.Infrastructure.csproj
--startup-project src/Nexus.Products.Chat.Api/Nexus.Products.Chat.Api.csproj --output-dir Sql/Migrations`
→ `Build succeeded. / Done.` (design-time model build only; no database was connected or migrated).

## Risks / notes

- **Update cannot clear-to-root over HTTP.** `null` on `UpdateBranchRequest.ParentBranchId` is
  deliberately treated as "leave unchanged" to keep existing PUTs byte-compatible. Clearing is
  possible at the domain (`ChangeParent(null)` — tested) and would need an explicit clear signal
  (wrapper/sentinel) on the HTTP contract if product requires it. Recorded as a follow-up, not done
  here (scope limit).
- **Failure HTTP semantics unchanged.** Self-parent / cross-conversation / cycle violations throw
  `InvalidOperationException` from handlers (mirrors `UpdateSessionHandler`). Without a global
  exception mapper these surface as 500; no new error-contract plumbing was introduced.
- **Cross-Conversation parent is enforced in the Application layer only.** The DB FK enforces
  existence of the parent row but not same-Conversation; Create and Update handlers enforce the
  conversation-scoped rule before persisting.
- **Migration not applied to any database.** `ef migrations add` only builds the model; applying
  the migration to a SQL database is outside this lane's scope (no DB connection was used/needed).
- **Self-FK delete behavior.** `Restrict` means a parent Branch with children cannot be deleted
  directly at the DB level; the only Branch delete path today is the `Conversation -> Branch`
  cascade, which is unchanged and still removes an entire conversation's tree.
- **Cosmetic.** Two handler files rewritten by the lane were authored with LF endings; git prints a
  CRLF-normalization warning on diff only. Nothing is staged or committed.
- Nothing was committed, pushed, or merged; no `git add .` was run; all changes remain working-tree
  only. No workbook exists in this repository and none was created or written.
