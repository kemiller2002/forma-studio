# 15 Fault handling, recovery, and Aegis

## Standard fault mechanism

FRA-001 The F# Studio engine MUST use EchelonFoundry.Aegis.Core as the standard mechanism for capturing and classifying unexpected operational failures where Aegis applies.

FRA-002 Expected domain refusals MUST remain ordinary typed transition outcomes and MUST NOT be converted into Aegis faults merely because an operation was refused.

FRA-003 Aegis MUST NOT replace Limen's typed browser effect outcomes.

FRA-004 Limen OutcomeUnknown MUST remain semantically distinct from an Aegis-classified failure.

FRA-005 Aegis integration MUST preserve the Ordo distinction between known failure, unknown effect outcome, rejected transition, and unresolved obligation.

## Fault boundaries

FRA-020 Aegis SHOULD capture unexpected failures at infrastructure/application boundaries including project parsing, migration execution, GitHub integration adapters, local persistence adapters, export generation, and WASM/interop boundaries.

FRA-021 Pure domain transitions SHOULD return typed domain results and MUST NOT depend on exception throwing for ordinary control flow.

FRA-022 Fault scopes MUST use stable operation names suitable for diagnostics.

FRA-023 Fault context MUST classify values by disclosure sensitivity before they reach sinks or diagnostics.

FRA-024 Project content MUST NOT be included wholesale in fault metadata.

FRA-025 Authentication tokens, secrets, raw credentials, and sensitive fixture values MUST be redacted or omitted.

## Presentation

FRA-040 User-facing fault presentation MUST explain the failed operation without exposing stack traces or internal secrets.

FRA-041 User-facing presentation SHOULD distinguish retryable, nonretryable, permission, unavailable, conflict, and unknown/reconciliation conditions when evidence supports that distinction.

FRA-042 A presented fault MUST NOT claim a remote write failed when the effect outcome is unknown.

FRA-043 Recovery actions MUST correspond to legal current-state capabilities.

FRA-044 The editor MUST preserve the last known valid project state after an unexpected operational fault.

## Recovery

FRA-060 Recovery policy MUST be explicit by operation class.

FRA-061 Non-idempotent remote writes MUST NOT be retried automatically after an unknown outcome.

FRA-062 Reads MAY be retried only under a bounded policy and when cancellation/staleness semantics are preserved.

FRA-063 Recovery attempts MUST reject stale results using operation/correlation identity where concurrent operations are possible.

FRA-064 Reconciliation MUST be available for unknown GitHub save outcomes before a duplicate write is attempted.

FRA-065 Offline/local-draft recovery MUST not overwrite a newer known remote revision without conflict handling.

## Diagnostics and sinks

FRA-080 Studio MUST function without requiring a network fault sink.

FRA-081 Diagnostic sinks MUST be replaceable behind Aegis contracts.

FRA-082 GitHub-backed fault storage MAY be enabled later but MUST be opt-in and privacy-reviewed for product use.

FRA-083 Product telemetry and repository ROS telemetry MUST remain distinct from Aegis runtime-fault records.

FRA-084 Fault diagnostics MUST include enough stable identity to correlate a user-visible error code with internal diagnostics without exposing project secrets.

## Testing

FRA-100 Tests MUST prove expected domain refusals do not become Aegis faults.

FRA-101 Tests MUST cover capture/classification at each adopted infrastructure boundary.

FRA-102 Tests MUST cover redaction of secrets and sensitive context.

FRA-103 Tests MUST cover recovery refusal after unknown non-idempotent outcomes.

FRA-104 Tests MUST cover presentation behavior with incomplete/unknown diagnostic information.

FRA-105 Aegis package version changes MUST run engine regression tests and fault serialization/compatibility checks.
