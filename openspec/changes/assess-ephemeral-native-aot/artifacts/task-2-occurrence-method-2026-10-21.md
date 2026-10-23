# Expanded task 2 occurrence reconciliation — 2026-10-21

Expression (case insensitive, one match per source line): `native\s*aot|\baot\b|trimm|orcacore-json-v1|fixed.*codec|serializer.*(?:hook|replac)|(?:replac|configur).*serializer|JsonSerializer|managed.heap|reflection`.
Scan the exact forty paths in task-2-owner-coverage; retain zero-hit owners.
The original 125 approved baseline rows are separately verified byte-exact by
path, current line and SHA-256. This expanded expression also includes reflection,
managed-heap and broader fixed-codec context; it does not rewrite the old ledger.

Current hits: 218; unchanged original rows: 125; owners: 40.
Each current row retains its exact excerpt, heading, line hash and individual
source-derived disposition. Added text is bounded assessment/status only;
existing runtime, fixed-codec and ownership statements remain unchanged.
