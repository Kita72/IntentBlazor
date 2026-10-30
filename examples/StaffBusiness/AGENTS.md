# IntentBlazor.StaffBusiness

Interactivity Mode: Server. Interactivity Scope: Per-page. Created with dotnet new blazor -int Server --empty.

Pages are static SSR by default. The workspace uses InteractiveServer with prerender disabled; keep Routes static. Sign-in uses static SSR and antiforgery-protected HTTP POSTs. Never use HttpContext in interactive components. EF Core uses IDbContextFactory and a context per operation; its domain database stays separate from framework checkpoints. Data and accounts are synthetic. Live models are optional and unverified. Keep domain behavior here and retain AiInput/AiOutput.
