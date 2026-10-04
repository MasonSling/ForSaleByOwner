# For Sale By Owner — Texas demo

.NET 10 Razor Pages + Tailwind CSS + HTMX + Alpine CSP build. A complete visual concept with fictional properties and a session-isolated in-memory seller workspace. No database, account, payment, LLM key, or external service is needed.

## Run

Prerequisites: .NET SDK 10.0.401 or a later patch in that feature band; Node.js with npm for build-time assets.

```powershell
npm ci
dotnet build
dotnet run --project src/ForSaleByOwner.Web --no-launch-profile --urls http://localhost:5080
```

Open http://localhost:5080. `npm run watch` watches Tailwind sources during development. Build and publish compile CSS and copy pinned HTMX/Alpine assets. The published app does not require Node.

```powershell
dotnet test
dotnet publish src/ForSaleByOwner.Web -c Release -o artifacts/publish
```

## Layers

- **Web:** HTTP, Razor partials, frontend behavior, composition root. Handlers delegate to BLL.
- **BLL:** sale workflows, models, document/listing/workspace interfaces, validation.
- **DAL:** in-memory workspace implementation, document catalog, fictional listings; future EF Core PostgreSQL implementations.
- **DB:** reserved migration/deployment project. See its README for the deployment contract.
- **Tests:** business behavior and HTTP integration tests.

The demo uses a protected, HttpOnly, SameSite cookie with a random session identifier. Mutable data is isolated by that identifier, protected from concurrent updates, and stored only in process memory. Closing the browser session, resetting the demo, or restarting the server can reset progress. It is not authentication. Do not use real personal information or deploy this as a real seller service.

## Routes and behavior

`/` marketing; `/start` intake; `/app` overview; `/app/agent`, `/app/documents`, `/app/listing`, `/app/offers`, `/app/closing`; `/homes` and `/homes/{id}`; `/developers` interface previews.

Try “What do I do next?”, “Explain lead disclosure”, and “Draft my listing”. Update year built or HOA status through intake to change the plan. Completing checklist tasks, editing copy, and demo publication affect only your demo workspace. Publishing never adds a listing to the public fixture catalog. Local photo previews never upload or persist. Signing states, delivery receipts, offers, and closing milestones are labeled illustrations.

The frontend uses encoded Razor output, antiforgery tokens, a restrictive script policy, Alpine's CSP-compatible build, and disabled HTMX history caching. Private demo pages use `Cache-Control: no-store`. All application assets are served locally.

## Future production work

Authentication and ownership checks must precede real documents. Add EF/Npgsql persistence, private object storage, background workflow execution, provider-backed signing and submission receipts, source-grounded OpenRouter tools, billing, and real public listing REST/MCP endpoints. Keep public listing data separate from private workspace records.

Official form links are references, not locally redistributed forms. Texas workflow applicability needs professional review before production use. Third-party services and free government forms are separate from the illustrative $299 guidance package.

## Bring your own AI

`/developers` presents the planned second revenue path: usage-based seller tools for connected assistants, alongside free public listing discovery. `/llms.txt` and `/agent-capabilities.json` expose the same availability and access boundaries as machine-readable documentation. The manifest is custom documentation, not an executable MCP or OpenAPI contract. Cloudflare Monetization Gateway / x402 is a researched direction, not integrated billing.

The page includes server-rendered copy, descriptive metadata, WebPage JSON-LD, and internal links. These support discoverability but do not guarantee Google placement or AI adoption. Before public launch, configure a real canonical domain and sitemap, validate indexing in Search Console, and replace previews with verified service contracts. `robots.txt` requests that crawlers avoid the demo workspace; it is not access control.

Photo attribution is in `src/ForSaleByOwner.Web/wwwroot/images/README.md`.
