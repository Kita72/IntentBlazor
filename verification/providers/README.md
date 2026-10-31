# Recorded local provider attempts

These are sanitized observations from opt-in synthetic evaluations, not a model certification. Root reports are consumed by `scripts/Verify-Readiness.ps1` when no newer local artifact exists. Keep the original source fingerprint, timestamps, model identity, budgets, measured outcomes and failures. Never relabel old observations as current-source passes.

The root Ollama and LM Studio reports record native read-tool profiles. The `preloaded/` reports are independent runs with data loaded directly by a C# service. They do not prove native model tool invocation. None of these runs passed all three workflows; the LM Studio preloaded news task alone completed its initial/follow-up documents, actual C# reads, local interaction and protected draft checks. Server/model file identities and limitations are described in the [provider matrix](../../docs/provider-evaluation.md).

OpenAI remains deferred by the maintainer. Do not manufacture an OpenAI report. Future maintainer-controlled evaluations must replace reports with actual current-source evidence before a release tag is created. Ordinary CI makes no model requests.
