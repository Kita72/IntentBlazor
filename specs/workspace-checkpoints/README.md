# Saved checkpoint compatibility fixtures

These authored synthetic records are stored checkpoint format `1`, with output schemas `0.1`, `0.2` and `0.3`. Each retains the same editable private draft, its original baseline and a completed conversation turn. They contain no real identity, credentials or private user content.

`FoundationHostTests.SavedCheckpointFixtures_RestoreSupportedSchemaDraftAndHistory` restores these files through the public SQLite/session APIs and the production recipe's fresh owner/data reauthorization. The current authorized note dataset replaces the cached rows. Do not regenerate these saved bytes from the current serializer during that test: the fixtures represent existing saved data.

Unknown checkpoint/output versions are tested separately and must not silently overwrite persisted bytes or discard a current local draft. The framework SQLite schema also has its own version guard. See the [production hosting and upgrade guide](../../docs/foundation-step4-production-hosting.md).
