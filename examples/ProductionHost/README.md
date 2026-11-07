# Production host integration recipe

An application-owned .NET 10 Interactive Server recipe for foundation Step 4. It demonstrates the host integration around the unchanged default pair:

~~~razor
<AiInput />
<AiOutput />
~~~

The [production hosting guide](../../docs/foundation-step4-production-hosting.md) explains the complete integration and operational boundary. This is source you adapt in your app, not a ninth framework package or a mandatory account system.

## Run without inference

~~~powershell
dotnet run --project examples/ProductionHost --no-launch-profile -- --environment Development --urls http://localhost:5184
~~~

The public introduction and `/health/ready` work without credentials. `/workspaces`, `/workspace/{Key}`, `/ai-settings` and private artifacts require authenticated host claims plus a current server account grant. There is intentionally no sign-in shortcut or seeded account. Cookies are Secure even in Development: real sign-in requires an HTTPS host/development endpoint. Integration tests use protected cookies on an in-memory HTTPS TestServer, with test-only authentication code outside this project.

The default input processor produces an authored private-note workspace from an authorized C# service. It makes no model call. `Ai:Provider` configuration does not activate inference. A developer may explicitly supply the `configureModel` callback in `ProductionHostApp.Build` or `AddHostWorkspace`; the same request limiter stays around the processor. The framework connection guide describes the provider adapters.

## Host configuration

`Host:DataDirectory` must be an absolute private directory outside the app/content tree in Production. Development defaults to ignored `artifacts/production-host`. Set `AllowedHosts` to your real public hostname. A trusted authentication provider must issue one authenticated identity with `ClaimTypes.NameIdentifier` and `tenant_id`. Give the tenant an issuer-qualified namespace when using multiple identity providers.

`Host:Accounts` is an empty-by-default **server permission directory**, not a login system. Populate it from trusted configuration for integration, or replace `IHostAccountDirectory` with your application's current account/permission store. Recheck account permissions and revocation there; let the real authentication integration enforce individual session expiration and revalidation. Never populate these claims or grants from browser fields, request headers, model arguments or generated content.

Production key encryption uses Windows DPAPI by default. Alternatively set `Host:KeyEncryptionCertificateThumbprint` for a valid certificate with its private key in the current user's certificate store, or supply the host-owned `protectKeys` callback. Non-Windows Production startup requires explicit key encryption. Set `Host:KnownProxy` only for an exact trusted immediate proxy; forwarded headers are not generally enabled. No secrets belong in committed configuration.

The application owns `business.sqlite` (notes, workspace names/keys and transactional write receipts) and `usage.sqlite` (daily accepted-request counts). Framework checkpoints/receipts use `workspaces.sqlite`; artifact bytes and encrypted key material have separate private subdirectories. This sample is for one application process on local persistent storage. It does not establish multi-node coordination or a production identity provider.

## Offline verification

`scripts/Verify.ps1` builds/tests the recipe and calls `scripts/Verify-ProductionHost.ps1` after packing the framework. The latter copies this app outside the repository, replaces all framework project references with local packages, compiles every page, renders default components/setup tools, and exercises a protected draft, durable conversation and authorized business save. The existing package-only quickstart separately checks the renderer-extension tutorial.

See `FoundationHostTests` and `FoundationHostHttpTests` for exact boundaries and failure cases. No local model service, paid API, public host or real identity account is used.
