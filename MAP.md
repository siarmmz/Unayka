# MAP

One line per file as "path: what it does". Check before searching the disk.

CLAUDE.md: project rules and Claude Code workflow (version 3)
README.md: product description, stack, configuration, development, phases and open decisions
TASKS.md: tasks by phase (In progress, Next, On hold, Done)
MAP.md: this file map
PLAN.md: context between sessions (ignored by Git)
.gitignore: files ignored by Git (includes the whole .claude folder)
.claude/agents/opus.md: subagent that analyzes, decides architecture and splits tasks (ignored by Git)
.claude/agents/sonnet.md: subagent that writes the code (ignored by Git)
.claude/agents/haiku.md: subagent for fast execution, research, documentation and tests (ignored by Git)
.claude/local/state.json: initialization state, rules version and skills (ignored by Git)
Dockerfile: multi stage build (Tailwind styles, .NET publish, ASP.NET runtime) on port 10000
.dockerignore: files excluded from the Docker build context
.editorconfig: C# formatting and naming rules (JetBrains Rider style)
src/Unayka.slnx: solution with Core, Web and the two test projects
src/Unayka.Core/Configuration/AppEnvConfig.cs: single typed reader of environment variables with production validation
src/Unayka.Core/Presentation/SafeText.cs: HTML escaping of visible text with a small inline tag allowlist
src/Unayka.Core/Presentation/SafeUrl.cs: URL safety (rejects javascript: and data:) and link target choice
src/Unayka.Core/Presentation/Formatters.cs: MZN money, Maputo date and +258 phone formatting
src/Unayka.Web/Program.cs: app startup, loads .env first, validates configuration, maps health and Razor components
src/Unayka.Web/.env.example: documented environment variables
src/Unayka.Web/_Imports.razor: the only place for common usings, @inherits and universal injections
src/Unayka.Web/Endpoints/HealthEndpoint.cs: /health reporting active database and storage
src/Unayka.Web/Components/Base/UnaykaView.cs: shared static view helpers (Text, ToUrl, formats)
src/Unayka.Web/Components/Base/UnaykaComponentBase.cs: base class for components exposing the view helpers
src/Unayka.Web/Components/Base/UnaykaLayoutBase.cs: base class for layouts exposing the same helpers
src/Unayka.Web/Components/Shared/SeoComponent.razor: head title, description, canonical, Open Graph and structured data
src/Unayka.Web/Components/Layout/MainLayout.razor: site header, navigation and footer
src/Unayka.Web/Components/Pages/Home.razor: home page with introduction and service list
src/Unayka.Web/Components/Pages/Services.razor: services page
src/Unayka.Web/Components/Pages/About.razor: about page
src/Unayka.Web/Components/Pages/Location.razor: location page (placeholder until address is defined)
src/Unayka.Web/Components/Pages/Contact.razor: contact page (placeholder until channels are defined)
src/Unayka.Web/Components/Pages/NotFound.razor: not found page
src/Unayka.Web/Components/Pages/Error.razor: generic error page
src/Unayka.Web/wwwroot/styles/globals.css: Tailwind entry with theme colors, fonts and base layer
src/Unayka.Core.Test: tests for configuration and presentation helpers
src/Unayka.Web.Test: in memory server tests for pages, health and the no loose text rule

## Interactive render modes

None yet.
