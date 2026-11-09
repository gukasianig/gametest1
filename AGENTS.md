# Repository Guidelines

## Start of Every Chat
Before changing this repository, read `docs/PROJECT_CONTEXT.md` (short current state). Read `docs/CHANGELOG.md` and `docs/DEVELOPMENT.md` only as far as the task needs: usually the first ~40 lines (newest entries/risks) or a grep for the feature. Check `git status --short -- Assets ProjectSettings Packages docs/*.md` (the full status is noisy with binary art files) and relevant source files against these notes; the code may have changed since the last update. Communicate with the owner in Russian.

## Token Budget (standing owner request, 2026-11-09)
- Never read `docs/archive/` unless a task needs old history; grep it instead of reading whole files.
- Keep active notes small: `PROJECT_CONTEXT.md` ≤ 10 KB (replace stale facts, do not append), `CHANGELOG.md` ≤ ~20 KB and `DEVELOPMENT.md` ≤ ~20 KB. When one grows beyond its limit, move the oldest dated entries to `docs/archive/<NAME>_<period>.md` and leave a one-line pointer.
- Large docs (`GAME_ROADMAP.md`, `UPGRADE_ROADMAP.md`, `VISUAL_ASSETS.md`, `IDEAS.md`, `MASTER_PLAN.md`): grep for headings/IDs and read only the needed range with offset/limit.
- Never read Unity-generated or binary files (`Library/`, `Logs/` beyond the needed lines, `.unity`/`.prefab`/`.asset` in full, `.fbx`, `.blend`, images); grep scene/prefab YAML for the needed object instead.
- Do not re-read a file just edited; prefer Grep/targeted ranges over full reads of large sources.

After meaningful work, update the relevant notes with the date, changed files, reasons, actual checks, and remaining issues. Distinguish observed behavior from assumptions. Never describe compilation alone as a gameplay test. Preserve existing user changes and Unity asset GUIDs. Keep credentials, tokens, license identifiers, and personal logs out of these documents.

## Required Context Maintenance
Maintain these notes as part of each task, without waiting for a reminder or asking permission:
- Before the final reply after changes, investigations, tests, or setup, update the affected documents: `PROJECT_CONTEXT.md` for current state and next steps, `CHANGELOG.md` for dated changes and decisions, `DEVELOPMENT.md` for verification and unresolved risks.
- Record important findings at stable checkpoints during long tasks; do not defer all documentation until the end. If work is incomplete, record what remains and the blocker.
- Keep entries brief, consolidate repeated facts, and replace obsolete current-state information. Do not append entire conversations or add entries for routine replies with no new project facts.
- Read only the notes and source needed for the current task; avoid repeating a full audit unless requested or justified by new evidence.
- Maintain `docs/GAME_ROADMAP.md` after related gameplay tasks: check and strike through completed items with a date and brief validation; leave partial/unverified work unchecked with the remaining work stated. Read only its nearest priorities and relevant section, not the whole backlog every task. Roadmap ideas do not authorize implementing every item or a full audit.
- Before finishing, check that notes match the actual changes and checks. This is agent-maintained documentation during active work, not a background file watcher; recheck external edits on the next task.

- For a partially completed roadmap item, keep the parent unchecked and add dated checked, struck-through subitems for finished portions with their actual verification scope; list remaining work separately. Do not hide implemented progress solely in prose or mark unverified gameplay complete.

- Overall plan: `docs/MASTER_PLAN.md` (stages with ✅/🟡/⬜/👁 statuses and the "next steps" list). After meaningful work update its stage statuses and next steps together with GAME_ROADMAP.md; read only its summary table and "Ближайшие шаги" at the start of a chat.

- For upgrade-related work, maintain the matching UP001-UP100 items in docs/UPGRADE_ROADMAP.md together with section D of GAME_ROADMAP.md. Read only its short next-step queue and the relevant group. Proposed upgrades stay unchecked until implemented and verified in the recorded scope; planning does not authorize implementing the entire catalog.

## Project Structure & Module Organization
This is a Unity 2022.3.62f3 C# game using Universal Render Pipeline (URP).
- `Assets/scripts/` contains player movement, shooting, health, enemy AI, waves, and orbiting weapons; `Interfaces/` contains shared damage and projectile contracts.
- `Assets/Scenes/GreenArenaPlay.unity` is the only game and build scene (SampleScene was removed on 2026-11-09; its git history keeps it). Gameplay prefabs currently live directly under `Assets/`.
- `Assets/Settings/` holds URP settings; `Assets/TextMesh Pro/` and `Assets/TutorialInfo/` contain supporting package and template assets.
- `Packages/manifest.json` defines dependencies; `ProjectSettings/` holds shared Unity configuration.
- `Library/`, `Temp/`, `Logs/`, and generated solution/project files are ignored. Do not edit or commit them.

## Build, Test, and Development Commands
Use the editor version recorded in `ProjectSettings/ProjectVersion.txt`. In PowerShell, set `$Unity` to that editor's `Unity.exe` path.
- `& $Unity -projectPath .` opens the project. Open `GreenArenaPlay` and press Play for local gameplay checks.
- `& $Unity -batchmode -quit -projectPath . -logFile import.log` imports assets and checks script compilation; inspect the log for errors.
- `& $Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile tests.log` runs Edit Mode tests; use `PlayMode` for runtime tests.
- Build through **File > Build Settings**, selecting the target platform and enabled scene. No custom command-line build method exists.

## Coding Style & Naming Conventions
Use four-space indentation and braces on separate lines. Prefer PascalCase for new classes and methods, camelCase for fields and parameters, and an `I` prefix for interfaces. Match new MonoBehaviour filenames to class names. Existing names are inconsistent; avoid unrelated renames. No formatter or linter configuration is present.

## Testing Guidelines
Unity Test Framework 1.1.33 is installed, but no test suite or coverage threshold exists. When tests are necessary, use `Assets/Tests/EditMode/` or `Assets/Tests/PlayMode/` with appropriate test assemblies and `<Feature>Tests.cs` names. Standing user instruction (2026-11-01): batch gameplay verification at the completion of a coherent system. For intermediate edits, inspect the changed code and direct dependencies; use compilation or focused automated checks when needed. Do not open or inspect Unity after every edit. At system completion, run one combined Play Mode check of that system and its direct dependencies; check earlier only for a concrete runtime uncertainty, reported bug, or explicit user request. Record deferred checks briefly. A full game audit or regression pass still requires an explicit user request. Repeat checks only after relevant edits, failures, or new evidence; keep screenshots and logs minimal. Never describe compilation alone as gameplay verification.

- Standing user preference (2026-11-01): progress through P0 in small, bounded tasks. Keep Unity checks focused and short; reuse available opt-in checks, avoid repeated screenshots/focus retries and do not rerun passed checks without relevant new evidence. For trivial static cleanup, state exactly what file checks establish and defer runtime confirmation to the next related combined check.

- When editing an already-open Unity scene externally, the editor may keep the old scene in memory even after script refresh or Play restart. Reload the saved scene at the next relevant verification (preserving unsaved user work), and distinguish the on-disk fix from what is currently displayed. Do not treat Assets Refresh alone as proof that scene edits are applied.

## Commit & Pull Request Guidelines
History uses short, informal subjects such as `add weapons`; no strict convention is established. Use concise imperative subjects describing the change. PRs should explain behavior, link relevant issues, report validation and blockers, and include screenshots or clips for visible changes. Commit Unity assets with their `.meta` files; preserve GUIDs and review scene/prefab changes for unintended edits.
