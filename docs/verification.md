# Demo verification

Verified October 4, 2026 on Windows with .NET SDK 10.0.401.

- 18 xUnit tests pass: all page routes, intake, the 1978 applicability boundary, HOA requirements, unsupported property types, agent drafts, editable listing persistence, draft publication invalidation, session isolation, reset, snapshot isolation, buyer filters, unknown IDs, antiforgery rejection, output encoding, no-store responses, and HTML partial responses.
- Release publish succeeds with locally bundled Tailwind CSS, HTMX, Alpine CSP, and photos.
- The published DLL serves the app without a database or Node runtime.
- Browser checks: HTMX agent reply and listing draft, buyer filter reducing results to one Austin home, intake submitted via keyboard, checklist changing from six to four tasks after year/HOA changes, Alpine mobile navigation, and browser-only photo preview.
- Responsive checks at 390px: intake, listing editor, and home search have no document-level horizontal overflow. Workspace navigation scrolls within its own row. Desktop landing page and seller views were visually inspected.
- No browser warnings or errors were observed during the checked interactions.

This is not a production security audit or a comprehensive accessibility audit. Authentication, real uploads, billing, live AI, signing, submissions, public API/MCP, and PostgreSQL persistence are intentionally absent.
