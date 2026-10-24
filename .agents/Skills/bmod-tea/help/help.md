# Test Architect knowledge

This document covers the skills of the `tea` module: what each one gives the user, when to recommend it, and what to offer next. Full documentation: <https://bmad-code-org.github.io/bmad-method-test-architecture-enterprise/llms.txt>

## The skills

- `bmad-tea`: Murat, the Master Test Architect, for risk-based test strategy, fixture architecture, API, UI, mobile and contract testing, and release gates. Recommend him when the user wants testing advice or does not know which TEA workflow fits; he routes to the rest.
- `bmad-tea-teach-me-testing`: teaches testing fundamentals in 7 sessions (TEA Academy), with a progress file, session notes and a completion summary. Recommend it to someone learning testing, at any phase.
- `bmad-tea-test-design`: risk-based test planning, producing a test design document. Recommend it during solutioning, before tests or a framework exist. Afterwards offer `bmad-tea-framework`.
- `bmad-tea-framework`: owns production-ready framework and CI setup, with framework-only, CI-only, or both scope. It installs Playwright Utils or Pact.js Utils when enabled. Recommend it during solutioning, after test design. When CI scope cannot be inferred from the request, interactive runs ask whether to include CI. Unattended runs default to framework only and state in the summary that CI was excluded.
- `bmad-tea-automate`: owns test generation in red or expand mode. Red mode writes failing acceptance tests and an ATDD checklist before implementation, then verifies the intended missing behavior. Expand mode writes tests for implemented code, runs them, heals test defects for up to three rounds by default, and reports product defects. Recommend red mode after a story is created and expand mode once code exists.
- `bmad-tea-evaluate`: builds and runs a scored, evidence-backed evaluation of an agent, skill, workflow or tool-use system. Recommend it after automation when the target's behavior needs proof. Afterwards offer `bmad-tea-framework` with CI scope to enforce it. On explicit request, Evaluate also analyzes saved native Test Design consistency offline or prepares a bounded fresh/configuration proposal. “Is it repeatable?” first clarifies saved records versus new runs. Native live experiments remain blocked by telemetry and enforceable-budget gaps; ordinary evaluation gains no extra experiment work.
- `bmad-tea-test-review`: audits test quality with a 0-100 score and a review report. Recommend it after tests are written. Afterwards offer `bmad-tea-trace`.
- `bmad-tea-nfr`: audits the evidence for non-functional requirements and writes an NFR report. Recommend it after automation, before a release.
- `bmad-tea-trace`: maps requirements to tests in a traceability matrix and makes the PASS, CONCERNS, FAIL or WAIVED gate decision. Recommend it after test review, for the release gate.

For a release gate, the order is: `bmad-tea-test-review` (optional), `bmad-tea-nfr` (optional), then `bmad-tea-trace`.

## Where things land

Every workflow writes under the test artifacts folder, chosen at setup as `modules.tea.test_artifacts`. Evaluate writes evaluation folders under `evals/` by default; a team changes that with `evaluations_folder` in `_bmad/custom/bmad-tea-evaluate.toml`. `bmad setup tea` shows and changes the setup answers.

## More detail

This document should be enough to route the user. Read a topic file only when the question is about its subject.

| Topic file             | Read when the user asks about                                                                                                |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `help/integrations.md` | What a TEA setup answer does, or installing Playwright Utils, Pact.js Utils, the Playwright CLI or MCP, or the SmartBear MCP |
