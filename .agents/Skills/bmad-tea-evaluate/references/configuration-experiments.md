# Optional saved-result and configuration experiments

Enter only after explicit experiment intent. Return directly after this route; activation and completion hooks stay suppressed. Ordinary Evaluate, native clean/mutated trials, native comparison and baseline acceptance keep their existing flow.

## Route by intent

- “Check how consistent the saved results from my latest Test Design evaluation are. Use only existing records. Do not rerun anything or make model calls.” means offline analysis.
- “Compare Test Design at medium versus high reasoning effort on the same model” means a live proposal with a spending boundary.
- “Compare Test Design model A versus B” means a one-factor proposal. “Compare one prompt-region change for Test Design” means a proposal that reports the unavailable prompt-region adapter before any spending approval.
- “Run a fresh repeatability experiment” means a live proposal.
- “Is it repeatable?” needs one clarification: existing saved results or fresh runs. Dispatch nothing before the answer.

## Offline analysis

Own discovery, input preparation, reader invocation and the report. Ask for the saved-run path or search root only when none is known from the conversation or evaluation output. Use the latest compatible records under that root and state every selected path. Do not ask the user to edit JSON, copy provenance or execute helper commands.

Run the installed `tea-evaluate experiment --search-root <root> --out <new-report-directory>`, or use `--saved <exact-directory>`. In this repository use `node cli/evaluate.js experiment`. These commands read saved bytes and replay the deterministic Test Design scorer; they launch no target, model, judge or subprocess. Never invoke preflight, run, custom hooks or capture to fill a gap in saved evidence.

Supported inputs are sealed legacy Test Design reader directories, native retained per-attempt directories and new experiment directories. Preserve original records and integrity files. For missing diagnostics, input identities or resources, report the resulting limitation. Use shipped ground truth only for matching native cases and input identity. An incompatible corpus needs its original companion ground truth through the legacy reader; this route cannot invent an adapter.

No retrospective success threshold is invented. Explain spread, counts, recorded native stability and deterministic scorer replay separately. Historical receipts stay historical. A descriptive finding, a native gate failure, an inconclusive result, selection clearance and a confirmed recommendation remain distinct.

## Live proposal and authority

Check capability and corpus feasibility before asking for spending approval. Current Claude/Codex adapters cannot prove applied effort or enforce an all-in money cap. Codex also lacks verified money telemetry. Explain these blockers and dispatch zero attempts. Subscription access supplies no verified zero-dollar budget. The native corpus has two independent cases; selection consumes both, leaving no unseen confirmation cases. Repetitions cannot repair this limit. No corpus expansion is authorized.

For a supported future mechanism or a labeled deterministic test double, batch consequential choices into one concrete plan approval: intended outcome, allowed quality loss or required improvement, critical failures, useful saving or latency limits, cases, repeats, retry bound, elapsed estimate, confirmation scope and verified all-in USD cap including overhead. Reuse previously approved thresholds only when they apply to the same identity. Missing decisions remain pending. Preparing a proposal grants no dispatch authority.

The controller freezes declaration, native input/scorer identities, gates, analysis, schedule, stopping rules and execution identity before approval. Approval binds the exact plan digest and cap. The local unsigned journal validates recorded lifecycle order and bindings. It cannot prove tail completeness, resist wholesale rechaining or establish independent wall-clock chronology. Exposure history covers this experiment; prior candidate exposure is unknown. Every dispatch reserves its verified bound first. Failed, partial, canceled, timed-out and retried attempts stay in the archive and spending ledger. Missing telemetry, rejected capture integrity, archival failure or an unresolved reservation stops further dispatch. Recovery reports preserve originals and partial archives, and never rerun an unsettled attempt.

The public CLI supports offline analysis, blocked native proposals through `--propose`, and reservation inspection through `--recover`. It supplies no approval or execution command. Approval and execution APIs currently serve the labeled deterministic integration tests; the native capability blockers prevent using those APIs to run providers.

The implementation's process doubles are labeled `deterministic-test-double`. They verify orchestration and capture and establish no provider quality or savings. Paid smoke runs require separate approval. Production changes, defaults, baseline acceptance and recommendation adoption each retain their own authority.

## Report

Give a short result, what the evidence establishes, what remains unknown, all-attempt cost and resource limits, and links to the selected records and report. Name blockers precisely and give the next supported action. Keep this route optional; never start it after a routine evaluation result without explicit intent.
