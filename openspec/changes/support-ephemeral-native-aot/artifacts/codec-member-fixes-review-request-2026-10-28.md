# Issue #13 codec-member fixes — local review target

Base: `1ad2a5946b2874fd709571376a67799f5c7f9cd1`, following approved implementation
checkpoint `8566986850dc377f5a69e165dd58936be50ad722`.
Branch: `codex/issue-13-native-aot-trimming`.

The target fixes both whole-AOT review findings and the P3 wording. Exactly three
product files change; public signatures, API/compile/dependency baselines and
canonical/delta requirements remain unchanged. The earlier publication preparation
is preserved, with current correction approval unset and its qualification pending.

Review the product diff, CodecMemberCases, schema-22/source-scope guards and
protocol-3 evidence rules. Check serialized-value rejection without extra getters,
write-only admission parity, failed-start/key/wait ownership and preservation of
the original synthetic-initializer and configuration negatives. The new record
does not inherit approval from the old source/native checkpoint.

Local lanes are green: OpenSpec 25/25, Release 0 warnings/errors, Infrastructure
309/309, unit 969/969, provider/integration 188/188, requirement selectors 60/60,
fifty CI-path stress runs 17,600/17,600 and engine coverage 79.3357%. Built ReSharper
reports zero errors for product, guard and correctly loaded fixture contexts.
The 13-case differential corpus agrees with pre-AOT master.

Windows 11 package preflights pass JIT 82, full-trim 55 and AOT 55. These do not
satisfy the required Server 2025/Ubuntu matrix. Protocol 3 requires 384 real-host
cases; native requalification awaits explicit temporary-branch authorization under
the later no-push instruction. No new native support approval is claimed.

Companion files: codec-member-fixes-validation-2026-10-28.json,
codec-member-fixes-self-review-2026-10-28.md and
codec-member-fixes-review-manifest-2026-10-28.txt. The local freeze receipt records
the exact candidate tree, content/status hashes and per-file bytes. No commit,
push, PR, issue edit, merge or release is authorized by this request.
