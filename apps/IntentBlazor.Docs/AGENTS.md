# IntentBlazor.Docs

This project uses per-page Interactive Server with prerendering and was created with dotnet new blazor -int Server --empty.

Pages are static SSR by default. Only add @rendermode InteractiveServer where a page needs interactivity. Do not put an interactive render mode on Routes.

Use Components/Pages for routes and focused shared components for reusable regions. Keep state explicit. Components run on the server when interactive; use IJSRuntime for browser APIs and do not depend on HttpContext during circuit interactions.

The public docs are anonymous. Authentication for examples is separate future work. Do not claim planned runtime features are implemented or add fake AI responses. Demos must label deterministic fixture mode.

Keep source links and contributor instructions accurate. Follow the root AGENTS.md and verification script.
