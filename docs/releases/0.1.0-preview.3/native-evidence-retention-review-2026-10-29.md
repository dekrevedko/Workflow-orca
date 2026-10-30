# Native publication evidence retention — self-review

Verdict: APPROVE, no blocking findings. Three checkpoint paths: the evidence ZIP
and the publication Markdown/JSON references. This verdict is a sole direct child.

GitHub rejects adding assets to the immutable preview.3 release. The six native
report artifacts, public job metadata and verification receipts are therefore
retained as a compact repository archive instead. The original package files,
SHA256SUMS, release source and tag are unchanged. No credential-bearing logs,
consumer binaries, build/cache output or local-user data are included.

The ZIP was read and every entry CRC checked. All six reports again pass the
protocol-3/explicit-version gate; their package hashes match the immutable assets.
The linked archive SHA-256 and JSON size/hash match. No product, C#, workflow,
requirement, API or pre-existing frozen evidence changed. Whitespace check passes.
The updated PR head still requires green CI before a merge commit.
