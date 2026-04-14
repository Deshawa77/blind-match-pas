# BlindMatchPAS Testing Evidence

## Final automated test result

- `dotnet build BlindMatchPAS.sln -p:UseAppHost=false`
  Result: build succeeded with `0` warnings and `0` errors.

- `dotnet test BlindMatchPAS.Tests\BlindMatchPAS.Tests.csproj --no-build`
  Result: `40` passed, `0` failed, `0` skipped.

## Coverage artifacts

- Raw assembly coverage:
  Artifact: [coverage.cobertura.xml](/C:/Projects/BlindMatchPAS/artifacts/TestResults/d9bd6b42-9a9b-49bc-8724-4e1d304e9058/coverage.cobertura.xml)
  Line coverage: `33.70%`

- Filtered application-logic coverage:
  Artifact: [coverage.cobertura.xml](/C:/Projects/BlindMatchPAS/artifacts/LogicCoverageFiltered/ec82cc2a-03cb-4b36-aa3e-00d59b908bec/coverage.cobertura.xml)
  Line coverage: `77.75%`

The filtered run excludes migrations, generated Razor output, `obj`, simple data containers such as models/view models/constants/options, and startup boilerplate so the percentage reflects the testable application logic more accurately.

## Unit tests

- [MatchServiceTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Unit/MatchServiceTests.cs)
  Covers the main blind-match workflow, including supervisor interest, identity reveal, and reassignment behavior.

- [MatchServiceEdgeCaseTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Unit/MatchServiceEdgeCaseTests.cs)
  Covers:
  - matching window closed
  - supervisor expertise mismatch
  - duplicate interest rejection
  - confirm match on an already-matched proposal
  - reassign to a supervisor without required expertise

- [SystemSettingsServiceTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Unit/SystemSettingsServiceTests.cs)
  Covers default supervisor-capacity behavior.

## Integration tests

- [StudentServiceTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Integration/StudentServiceTests.cs)
  Covers proposal creation and valid withdrawal against a real EF Core relational database.

- [ApplicationDbContextConstraintTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Integration/ApplicationDbContextConstraintTests.cs)
  Covers duplicate supervisor-interest protection.

- [DataRuleIntegrationTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Integration/DataRuleIntegrationTests.cs)
  Covers:
  - group-owned proposal persistence
  - one match per proposal
  - unique research area names
  - withdraw blocked after matching

- [PlatformPersistenceTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Integration/PlatformPersistenceTests.cs)
  Covers:
  - audit log persistence
  - notification log persistence
  - automatic system-settings row creation

- [SqlServerMigrationSmokeTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Integration/SqlServerMigrationSmokeTests.cs)
  Verifies `Database.MigrateAsync()` on a fresh SQL Server LocalDB database and confirms production migrations create the expected schema.

## Functional tests

- [AuthAndJourneyTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Functional/AuthAndJourneyTests.cs)
  Covers:
  - student login and proposal submission
  - supervisor login and blind-review interest flow
  - admin login and system-settings update
  - identity reveal to both sides after confirmation
  - RBAC boundary checks

- [AdminJourneyTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Functional/AdminJourneyTests.cs)
  Covers:
  - admin creates, edits, and deletes users
  - admin research-area CRUD
  - admin match reassignment

- [SecurityJourneyTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Functional/SecurityJourneyTests.cs)
  Covers:
  - self-registration disabled mode
  - password-reset flow
  - email-confirmed-required sign-in flow
  - optional two-factor authentication setup and login

- [GroupAndAnonymityJourneyTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Functional/GroupAndAnonymityJourneyTests.cs)
  Covers:
  - group lead workflow
  - group member visibility after identity reveal
  - explicit anonymity checks on the supervisor browse page

- [RoleWorkflowJourneyTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Functional/RoleWorkflowJourneyTests.cs)
  Covers:
  - student edit and withdraw flow
  - matched proposal edit blocking
  - group update flow
  - supervisor expertise setup and dashboard rendering
  - admin dashboard and allocation dashboard rendering

## Mocking evidence

`Moq` is used to isolate service logic from infrastructure dependencies, especially in:

- [MatchServiceTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Unit/MatchServiceTests.cs)
- [MatchServiceEdgeCaseTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Unit/MatchServiceEdgeCaseTests.cs)
- [SystemSettingsServiceTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Unit/SystemSettingsServiceTests.cs)
- [DataRuleIntegrationTests.cs](/C:/Projects/BlindMatchPAS/BlindMatchPAS.Tests/Integration/DataRuleIntegrationTests.cs)

This provides direct evidence of mocked collaborators such as settings, audit logging, user-directory lookups, and notification behavior.

## Execution commands

```powershell
dotnet build BlindMatchPAS.sln -p:UseAppHost=false
dotnet test BlindMatchPAS.Tests\BlindMatchPAS.Tests.csproj --no-build
dotnet test BlindMatchPAS.Tests\BlindMatchPAS.Tests.csproj --no-build --collect:"XPlat Code Coverage" --results-directory artifacts\TestResults
dotnet test BlindMatchPAS.Tests\BlindMatchPAS.Tests.csproj --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory artifacts\LogicCoverageFiltered
```

## Suggested manual demonstration checks

1. Log in as a student and create both an individual proposal and a group-owned proposal as a group lead.
2. Log in as a supervisor, set expertise, and confirm the browse page shows title, abstract, and technical stack without revealing student identity.
3. Express interest in a proposal and confirm the status moves from `Pending` to `Under Review`.
4. Confirm a match and verify the reveal page shows student details only after confirmation.
5. Log in as the student or a group member and verify supervisor name and email become visible only after the reveal.
6. Log in as the coordinator and manage users, research areas, allocations, and system settings.
7. Open the public register route and verify it stays disabled while coordinator-managed account creation remains the only onboarding path.
8. Enable confirmed-account sign-in and optional 2FA, then demonstrate both security flows.
