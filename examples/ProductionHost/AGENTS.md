# Production host integration recipe

This is application-owned sample code, not a new framework package or mandatory identity system. Keep the normal workspace page on the default AiInput/AiOutput pair. Use trusted authenticated claims and fresh server-side grants; never offer a synthetic sign-in or accept identity headers in this app. Tests may substitute authentication only in the test assembly.

Default to authored offline output without a model client. Every configured input processor must remain behind HostLimitedInputProcessor. Persist workspaces, artifacts, application records and admission counts outside the public/content directories. Preserve cancellation, identity rechecks, expected-version checks and idempotent business writes. No live provider execution, deployment or repository automation is authorized.
