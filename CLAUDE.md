# CLAUDE.md

Project rules version: 8

Unayka (U, N, A, Y, K, A) is a modern online shop of natural products: teas (coffee is one of them) and natural cosmetics, online and at the counter, in the same system. It is not a salon; never describe it as one. Market: Mozambique, currency MZN, UI in Mozambican Portuguese, payments by mobile wallet via PaySuite. Any spelling such as Unaika, Unaica or Unayca must be read and written as Unayka.

The full product description, stack, configuration, phases and open decisions are in README.md. Check MAP.md before searching the disk.

## Language

Everything related to programming is written in English: code, identifiers, routes, test names, XML docs, comments, commit messages, and all context and rule files (CLAUDE.md, TASKS.md, MAP.md, PLAN.md, README.md, agent definitions). Portuguese is used only for UI content shown to users (first argument of Text and SEO texts). Talk to the user in Portuguese.

## Stack and structure

.NET 10, Blazor Web App, EF Core, Tailwind CSS v4 (CLI), DotNetEnv, Npgsql (production) with SQLite fallback, S3 with local folder fallback, multi stage Docker on port 10000.

No appsettings.json, no launchSettings.json, no infrastructure project. Core holds rules and implementations (organized by feature), Web holds Blazor only. Core never references Web. Web contains no business rules.

All configuration comes from environment variables, read only by AppEnvConfig (Unayka.Core). DotNetEnv loads .env on the first line of Program.cs without overriding existing variables. Secrets never go to logs or code.

## Comments

Avoid comments. Write one only when there is real risk of confusion, short. Simple one sentence summary on interfaces and their members. Type summary only on classes and records that implement nothing (entities, options, DTOs). Classes implementing an interface get no summary and no inheritdoc. Blazor components get no documentation.

## Clean code

Intention revealing names, small single purpose functions, no magic numbers or strings, no dead code, no duplication, early returns, dependencies by interface and injection. Prefer record, tuples and lists. Classes only when the framework requires it. C# formatting follows JetBrains Rider via .editorconfig, applied before finishing each task. Each project file enables nullable, analyzers and TreatWarningsAsErrors; there is no Directory.Build.props.

## Blazor

Markup never contains logic or loose text. Everything displayed is prepared in the @code block and the markup only renders, always through Text(...). Data that never changes lives in private static readonly fields. A mutable field (_camelCase) only when the value really changes.

UnaykaComponentBase exposes Text, ToUrl, ToUrlTarget, FormatMoney (MZN), FormatDate and FormatPhone (+258). UnaykaLayoutBase exposes the same with the logic in a shared static class. Every layout declares @inherits UnaykaLayoutBase. The root _Imports.razor of Unayka.Web is the only place for common usings, @inherits and universal injections. SeoComponent is mandatory on every public page.

Public pages use static server rendering only. No global render mode. InteractiveServer only where there is real interactivity (cart, checkout, point of sale, administration), and each use is listed in MAP.md with the reason.

## Styles

Only Tailwind utilities in markup and src/Unayka.Web/wwwroot/styles/globals.css. Forbidden: .razor.css, style blocks, style attributes or any other CSS. app.css is generated, ignored by Git and never edited.

Every color comes from a theme token in globals.css (for example --color-accent, --color-surface, --color-ink), brand colors such as Google and Facebook included. No Tailwind default palette, no white or black literals and no hex values in markup, so changing one token changes the whole site.

## Responsive

Every page works on mobile, tablet and desktop. Prefer one responsive element. Only as a last resort render separate blocks per breakpoint, always from the same data prepared in @code.

## Look and feel

Minimalist, modern online store, lots of white space, lively natural palette on light surfaces, handwritten font on highlights, mobile first. No emojis, no em dashes, no decorative hyphens, no typographic bullets, no version badges in the UI and nothing that suggests AI generated content. No mention of AI or Claude in code, texts, commits or trailers.

## Security

Rate limiting with separate policies (login, webhook, checkout), antiforgery, Secure, HttpOnly and SameSite cookies, HSTS, security headers and a Blazor compatible CSP, persistent data protection keys, input validation, roles (Customer, Cashier, Manager, Administrator), logs without sensitive data. Immutable, insert only audit log. Cart totals are always recalculated on the server.

## Working with Claude Code

Absolute rule: nothing requested is executed directly unless explicitly stated (for example "execute directly"). Every request is analyzed first by Opus, written to TASKS.md and only then executed.

### Models

Aliases opus, sonnet and haiku. Opus thinks and decides (architecture, critical decisions, analysis and task splitting). Sonnet writes the code. Haiku does fast execution and analysis, research, documentation reading, tests and mechanical tasks. Opus and Sonnet use adaptive thinking when available, otherwise medium effort. Subagents live in .claude/agents.

### Context files

CLAUDE.md (versioned): rules, version at the top. MAP.md (versioned): numbered list, one line per file as "N. path - what it does", sorted by path and renumbered on every change. It is read before any search on disk and is updated in the same commit whenever a file is added, removed, renamed or changes purpose. TASKS.md (versioned): only work still to do, in In progress, Next and On hold (with condition); a finished item is removed, never kept as done. PLAN.md (ignored): context between sessions. .claude/local/state.json (ignored): initialization, rules version and skill maintenance. The .claude folder as a whole is ignored.

The whole .claude folder (agents, local state, settings) is ignored by Git. It is never committed. The primary agent always reviews it at the start of each session and checks that it matches this file: agent definitions follow these rules, state.json holds the current claudeMdVersion, and nothing in it contradicts CLAUDE.md. Any mismatch is fixed before other work starts.

At the start of each session the primary agent compares claudeMdVersion in state.json with the version in this file. If they differ, it rereads the rules, regenerates what is needed (MAP.md, tasks, configuration), adapts its behavior and only then updates state.json.

### Primary agent and worktrees

The primary agent runs in the main worktree without the WORKTREE_AGENT flag. It is the only one that sets up the environment, compares versions, updates state.json, MAP.md and TASKS.md, creates worktrees and splits tasks. Opus splits by feature folder so two agents never touch the same file.

Every prompt sent to a worktree starts with WORKTREE_AGENT=true (and the environment variable), and carries the branch, the TASKS.md item, the allowed files and the completion criteria. The agent confirms it is in a secondary worktree by comparing `git rev-parse --git-dir` with `git rev-parse --git-common-dir`. It only executes the assigned task, does not check the environment, does not regenerate MAP.md, does not validate the version, does not touch skills and only marks its own item in TASKS.md. If it needs a file outside scope, it stops and reports. When finished it delivers a short summary of created files.

When any task finishes, recommend running /clear before the next one.

### Skills

The primary agent keeps project skills suited to the work and records them in state.json. Every 24 hours it runs a review: Haiku lists skills and usage, Opus decides, Sonnet applies, the result goes into TASKS.md. A skill unused for 30 days and not protected is removed.

Protected (essential) skills: none defined yet.

## Git

Commit at every task. Each commit is one meaningful, complete unit of work that solves a whole problem and leaves the repository in a working state, for example "Add environment configuration with production validation" including its code and tests. Never commit an empty file, a single line, or a tiny addition on its own, and never split one coherent change into fragments. Do not make one huge commit for unrelated things either: group by problem solved.

If the history ends up fragmented or meaningless, rewrite it before sharing (rebase or recreate commits) so each commit reads as a plausible, self contained change.

Commit messages are written in English, short and in the imperative. Never mention AI, Claude, assistants or generated content in commit messages, and never add co-author trailers for them.

Never push, and never ask whether to push. Push happens only when the user explicitly asks for it, and never automatically. The workflow ends at the commit.

## Payments

Before implementing PaySuite, Haiku reads the official documentation and records in TASKS.md what was confirmed. Do not invent endpoints, fields or signature formats. The data model is provider agnostic.
