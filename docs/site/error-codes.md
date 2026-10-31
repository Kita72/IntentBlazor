# Framework error code index

Generated from literal validator, session, and setup failure call sites by [Export-ErrorReference.ps1](../../scripts/Export-ErrorReference.ps1). Run it after changing a code; verification detects drift. Codes identify a failure category; the returned path and message explain the specific condition. Application-defined actions and renderer contracts may add their own messages/codes.

See [errors and recovery](errors.md) for recovery rules, action outcomes, and normalized provider/content failures. Links point to the canonical source for each code, including its current bounds and message. This is a source index, not a promise of every possible host exception.

| Code | Canonical source |
| --- | --- |
| `action-argument-type` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `action-arguments` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `action-busy` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `action-properties` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `action-required` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `action-resource` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `action-version` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `artifact-reference` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `binding` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `binding-event` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `binding-field` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `binding-selection` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `binding-source` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `binding-state` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `busy` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs), [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `button-interaction` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `cancelled` | [ModelSetup.cs](../../src/IntentBlazor.AI/ModelSetup.cs), [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs), [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `chart-fields` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `chart-node` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `child-reference` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `condition` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `content-binding` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `data-key` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `data-schema` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `data-value` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `dataset-identity` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `dataset-reference` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `datasets` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `depth-limit` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `dirty-state` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs), [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `disposed` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs), [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `duplicate-key` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `effect-target` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `effect-type` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `effect-value` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs), [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `empty-effect` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `empty-session` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `event-kind` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs), [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `event-record` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `event-source` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `extension-config` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `extension-effects` | [AiSession.Extensions.cs](../../src/IntentBlazor.Runtime/AiSession.Extensions.cs) |
| `extension-event` | [AiSession.Extensions.cs](../../src/IntentBlazor.Runtime/AiSession.Extensions.cs) |
| `extension-fallback` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `extension-limit` | [AiSession.Extensions.cs](../../src/IntentBlazor.Runtime/AiSession.Extensions.cs) |
| `extension-node` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `extension-state` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `extension-unavailable` | [AiSession.Extensions.cs](../../src/IntentBlazor.Runtime/AiSession.Extensions.cs) |
| `extension-version` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `gallery-field` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `history-quota` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `identity` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `image` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `image-binding` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `image-state` | [ImageEditorContract.cs](../../src/IntentBlazor.Packs.ImageEditor/ImageEditorContract.cs) |
| `incompatible-workspace` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `input-failed` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `input-limit` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `input-node` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `input-rejected` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `input-state` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `input-type` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `input-value` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `interaction` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `interaction-identity` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `interaction-limit` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `interaction-reference` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `invalid-checkpoint` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `invalid-json` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs), [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs), [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `label` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs), [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `label-field` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `leaf-children` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `literal` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `missing-document` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `node-cycle` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `node-dataset` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `node-identity` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `node-limit` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `node-properties` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `node-version` | [DocumentValidator.Extensions.cs](../../src/IntentBlazor.Core/DocumentValidator.Extensions.cs) |
| `persistence-failed` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `persistence-unavailable` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `rating` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `restore-references` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `root` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `row-limit` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `schema-version` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `selection` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `setup-budget` | [ModelSetup.cs](../../src/IntentBlazor.AI/ModelSetup.cs) |
| `setup-busy` | [ModelSetup.cs](../../src/IntentBlazor.AI/ModelSetup.cs) |
| `setup-denied` | [ModelSetup.cs](../../src/IntentBlazor.AI/ModelSetup.cs) |
| `setup-invalid` | [ModelSetup.cs](../../src/IntentBlazor.AI/ModelSetup.cs) |
| `setup-unsupported` | [ModelSetup.cs](../../src/IntentBlazor.AI/ModelSetup.cs) |
| `sort-field` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs) |
| `stale-revision` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs), [AiSession.Extensions.cs](../../src/IntentBlazor.Runtime/AiSession.Extensions.cs), [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `state-dataset` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `state-definition` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `state-limit` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `state-range` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `state-value` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `table-fields` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `toolkit` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `unreachable-node` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `unsupported-action` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `unused-interaction` | [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs) |
| `workspace-access` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `workspace-conflict` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `workspace-owner` | [AiSession.cs](../../src/IntentBlazor.Runtime/AiSession.cs), [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
| `workspace-unavailable` | [AiSession.Workspaces.cs](../../src/IntentBlazor.Runtime/AiSession.Workspaces.cs) |
