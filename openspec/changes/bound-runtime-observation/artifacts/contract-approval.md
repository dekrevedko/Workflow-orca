# Durable workflow output observation contract review

Verdict: ACCEPT
Scope: bound-runtime-observation contract and owner routing
Reviewed proposal checkpoint: `b857386dd7d43bbe1d0a51e9ce8e331fa4b17b82`
Reviewed proposal tree: `ec1a9b19e1e73d28645c1bd6259a67880838bcca`
Reviewed normalized contract SHA-256: `f6c715c0f33770d198885a10d09116fc45287a257590fee2aa4eaeb908b34594`

The independent contract reviewer accepted the five requirement replacements after checking the current, predecessor and delta hashes and all eight document transformations. The accepted text distinguishes the DAG's initial read from its pending versioned subscription. It bounds scheduling after a pending durable workflow read and allows provider delay, including cleanup of in-flight I/O. A missing projection has its existing typed outcome; later query and checkpoint failures propagate. This review covers the contract and ownership. Source behavior, cost and qualification require separate evidence.
