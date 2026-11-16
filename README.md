# Company API (.NET 10) + Angular frontend

A small REST API that stores companies (name + website) in memory. It accepts a company only when its name is *relevant* to its website, using an explainable relevance model. An Angular app lets you create companies, list them and search them.

## Quick start

> **Requires the .NET 10 SDK and Node.js 22** (Angular 22 needs Node ≥ 20.19).
> Downloads: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) · [Node.js 22 LTS](https://nodejs.org/en/download). Check with `dotnet --version` and `node --version`.

```bash
# 1. Backend: http://localhost:5000 and https://localhost:7000
cd src/CompanyApi.Api
dotnet run                       # or, from the repo root: dotnet run --project src/CompanyApi.Api

# 2. Frontend: http://localhost:4200 (in a second terminal)
cd frontend/company-app
npm install
npx ng serve

# 3. Try the API (no token needed by default)
curl -i -X POST http://localhost:5000/api/companies -H "Content-Type: application/json" \
  -d '{"name":"MyCompany","website":"https://www.mycompany.com"}'
curl http://localhost:5000/api/companies
```

- **Swagger UI:** http://localhost:5000/swagger (Development only).
- **Defaults chosen to match the exercise:** data is kept **in memory only** (no database) and **authentication is off**, so the API can be used straight away from the frontend, Postman or curl. Both are configuration switches; see [Configuration](#configuration).
- **HTTPS (port 7000)** uses the ASP.NET Core dev certificate. Trust it once with `dotnet dev-certs https --trust`, or use plain HTTP on port 5000.

```
CompanyApi.sln
src/CompanyApi.Api              Controllers, Program.cs, DTOs (Contracts/), error mapping, auth, DI wiring
src/CompanyApi.Application      Domain model, ICompanyService/CompanyService, validator, relevance model, interfaces
src/CompanyApi.Infrastructure   InMemoryCompanyRepository (default), EfCompanyRepository (optional), HttpWebsiteContentFetcher
tests/CompanyApi.UnitTests      Validator, relevance, service, repository contract, mapping, auth, content-fetcher tests
tests/CompanyApi.IntegrationTests  API tests with WebApplicationFactory (both persistence providers, auth on and off)
frontend/company-app            Angular 22 app (standalone components, reactive forms, HttpClient)
```

## Configuration

All switches live in `src/CompanyApi.Api/appsettings.json` and can be overridden with environment variables (`__` separates sections, e.g. `Auth__Enabled=true`).

| Setting | Default | Values |
|---|---|---|
| `Persistence:Provider` | `InMemory` | `InMemory`: a private `ConcurrentDictionary`, no database involved. `EfInMemory`: EF Core with its InMemory provider, the same code path a real database would use. Either way, data is lost when the API restarts. |
| `Auth:Enabled` | `false` | `true` makes every `/api/companies` endpoint require a bearer token; see [Authentication](#authentication). |
| `Relevance:Threshold` | `0.5` | Minimum relevance score to accept a company. |
| `Relevance:SearchMinScore` | `0.3` | Minimum score for a company to appear in `q` searches. |
| `Relevance:EnableContentInspection` | `false` | Also read the website's title and meta tags (see [Relevance strategy](#relevance-strategy)). |
| `Cors:AllowedOrigins` | `http://localhost:4200` | Origins allowed to call the API from a browser. |

Example, both optional features on:

```bash
cd src/CompanyApi.Api
Persistence__Provider=EfInMemory Auth__Enabled=true dotnet run          # bash
$env:Persistence__Provider="EfInMemory"; $env:Auth__Enabled="true"; dotnet run   # PowerShell
```

## Frontend

The API base URL is set in `src/environments/environment.ts` (`http://localhost:5000/api`) and injected through the `API_BASE_URL` token.

The app has a sidebar and four lazy-loaded routes (`src/app/app.routes.ts`):

| Route | Page | Contents |
|---|---|---|
| `/add` (default; `/` and unknown paths redirect here) | `AddCompanyPage` | Create form + a **Search company** button that goes to `/search` |
| `/search` | `SearchCompaniesPage` | Debounced search, refresh and results table + an **Add company** button that goes to `/add` |
| `/docs` | `DocumentationPage` | API reference: endpoints (curl example and every response per endpoint), payloads, validation rules with their exact messages, and the relevance model (steps, weights, threshold, examples) |
| `/login` | `LoginPage` | Only used when the API has `Auth:Enabled = true`; otherwise it redirects to `/add` |

At startup the app calls `GET /api/auth/config`. With authentication off, `/add` and `/search` are open and the sidebar shows no sign-in or log-out. With it on, those pages require signing in.

**Responsive layout.** Above 768px the sidebar is fixed on the left. At 768px and below it becomes a top bar with a menu button; the menu closes after each navigation. At 640px and below, search results show as stacked cards instead of a table, and form buttons span the full width.

## Authentication

Off by default (`Auth:Enabled = false`). The code stays in place so it can be switched on without changes.

- **Turning it on:** set `Auth:Enabled=true`. In Development the signing key and the demo user **`admin` / `Admin123!`** come from `appsettings.Development.json`. In any other environment the API refuses to start without an `Auth:SigningKey` of at least 32 characters; provide it and the users through user secrets or environment variables (`Auth__SigningKey`, `Auth__Users__0__Username`, `Auth__Users__0__PasswordHash`). To hash a new password, call `PasswordHasher.Hash("…")` in `src/CompanyApi.Api/Auth/PasswordHasher.cs`.
- **API:** JWT bearer tokens signed with HMAC-SHA256 (60-minute lifetime). Passwords are stored as PBKDF2-SHA256 hashes (100,000 iterations) and compared in constant time. Unknown users and wrong passwords get the same answer, and sign-in is limited to 10 attempts per minute per IP address (429 afterwards). When authentication is off, `[Authorize]` stays on the controllers and the default authorization policy simply allows anonymous callers.
- **Frontend:** `AuthService` keeps the session in `sessionStorage`, so it survives a reload but ends when the tab closes. An HTTP interceptor adds the token only to requests for the API base URL. If the API answers 401 (e.g. the token expired), the interceptor signs the user out and sends them to `/login?reason=expired`. The post-login redirect accepts only paths inside the app.
- **Limits of this simple layer:** users live in configuration (no registration, roles or password reset), there are no refresh tokens (sign in again after 60 minutes), and a token cannot be revoked before it expires. A real deployment would use an identity provider (OIDC) or ASP.NET Core Identity with a database.

## Test

```bash
dotnet test                         # from the repo root: unit + integration tests
cd frontend/company-app && npx ng test --watch=false
```

Angular 22's default test runner is Vitest with jsdom, run by `ng test`. Its API is Jest-compatible (`describe`, `it`, `expect`, `vi.fn`).

## API

| Method | Route | Success | Errors |
|---|---|---|---|
| POST | `/api/companies` | 201 + `Location` + `CompanyResponse` | 400 validation / relevance / malformed body |
| GET | `/api/companies?name=&domain=&q=&page=&pageSize=` | 200 array (possibly `[]`) | 400 invalid query |
| GET | `/api/companies/{id}` | 200 `CompanyResponse` | 404 unknown id, **400 when `{id}` is not a GUID** |
| GET | `/api/auth/config` | 200 `{ "enabled": false }` | — |
| POST | `/api/auth/login` | 200 `LoginResponse` (bearer token) | 400 missing fields, 401 wrong credentials, 404 auth disabled, 429 more than 10 attempts/min |
| GET | `/api/auth/me` | 200 `{ "username": … }` | 401 without a valid token |

When `Auth:Enabled` is `true`, the `/api/companies` endpoints require `Authorization: Bearer <token>`, and a missing, invalid or expired token returns **401** with an `ErrorResponse`.

- `name` matches any part of the company name, ignoring case and accents.
- `domain` matches the website host and ignores `www.`. It also accepts subdomains and full URLs.
- `q` is a relevance search across name and domain. Results are ordered by score, highest first, and include `score` and `explanation`. Companies scoring below `Relevance:SearchMinScore` (0.3) are left out.
- `page` and `pageSize` are optional. Without `pageSize`, every result comes back. With it, the `X-Total-Count` response header carries the total number of matches. `pageSize` must be 1–100.

Every error uses the same body, `ErrorResponse`:

```json
{ "message": "Company name must be at least 2 characters.", "errors": ["Company name must be at least 2 characters."] }
```

Unexpected exceptions return **500** with `{"message":"An unexpected error occurred. Please try again later."}`. The details are logged at Error level and never returned to the client.

### curl examples

```bash
B=http://localhost:5000/api/companies

# POST: success (201 + Location)
curl -i -X POST $B -H "Content-Type: application/json" -d '{"name":"MyCompany","website":"https://www.mycompany.com"}'
# HTTP/1.1 201 Created
# Location: http://localhost:5000/api/companies/5f8aa2db-5162-482b-a0ad-19b3f5d98456
# {"id":"5f8aa2db-5162-482b-a0ad-19b3f5d98456","name":"MyCompany","website":"https://www.mycompany.com"}

# POST: validation errors (400)
curl -X POST $B -H "Content-Type: application/json" -d '{"name":"","website":"https://www.mycompany.com"}'
# {"message":"Company name is required.","errors":["Company name is required."]}
curl -X POST $B -H "Content-Type: application/json" -d '{"name":"MyCompany","website":"ftp://mycompany.com"}'
# {"message":"Website URL scheme must be http or https.", ...}

# POST: irrelevant name/website pair (400, with the explanation)
curl -X POST $B -H "Content-Type: application/json" -d '{"name":"MyCompany","website":"https://www.google.com"}'
# {"message":"Company name 'MyCompany' does not appear to be relevant to website 'https://www.google.com'.",
#  "errors":[..., "Name tokens [mycompany] vs domain 'google': domain match 0.00, token overlap 0.00, fuzzy similarity 0.43 -> offline score 0.17."]}

# POST: malformed JSON (400)
curl -X POST $B -H "Content-Type: application/json" -d '{"name": "MyCompany",'
# {"message":"The request body is empty or is not valid JSON.", ...}

# GET list: success, filters, relevance search
curl $B                          # [] when empty, otherwise every company
curl "$B?name=comp"              # [{"id":"…","name":"MyCompany","website":"https://www.mycompany.com"}]
curl "$B?domain=mycompany.com"   # same
curl "$B?q=acme"                 # [{"id":"…","name":"Acme Tools Inc.","website":"https://acmetools.com","score":0.7889,"explanation":"…"}]
curl "$B?name=zzz"               # []

# GET list: invalid query (400)
curl "$B?pageSize=0"             # {"message":"pageSize must be between 1 and 100.", ...}

# GET by id: success, not found, bad format
curl $B/5f8aa2db-5162-482b-a0ad-19b3f5d98456   # 200
curl $B/00000000-0000-0000-0000-000000000001   # 404 {"message":"Company '…' was not found.", ...}
curl $B/not-a-guid                             # 400 {"message":"'not-a-guid' is not a valid company id (expected a GUID).", ...}
```

With `Auth:Enabled=true`, sign in first and send the token:

```bash
API=http://localhost:5000/api
curl $API/auth/config                          # {"enabled":true}
TOKEN=$(curl -s -X POST $API/auth/login -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | sed -E 's/.*"accessToken":"([^"]+)".*/\1/')
curl -H "Authorization: Bearer $TOKEN" $API/companies   # 200
curl $API/companies                                     # 401 {"message":"Authentication is required. ...", ...}
curl -X POST $API/auth/login -H "Content-Type: application/json" -d '{"username":"admin","password":"nope"}'
# 401 {"message":"Invalid username or password.", ...}
```

## Validation rules

Both server rules and client rules apply. `CompanyInputValidator` is the authoritative server implementation, and the Angular validators mirror it.

- **Name:** required, not whitespace-only, trimmed, `NameMinLength = 2`, `NameMaxLength = 200`.
- **Website:** required, absolute, well-formed, scheme `http` or `https`, and a host containing a dot. `https://localhost` and `http://example` are rejected.
- **Relevance:** the score must be ≥ `Relevance:Threshold` (default **0.5**, in `appsettings.json`).

## Relevance strategy

The strategy is `IRelevanceStrategy` → `CompositeRelevanceStrategy`. It returns `RelevanceResult(Score, Explanation)`, where the score is in [0, 1]. It is deterministic and works offline by default.

1. **Normalize** (`TextNormalizer`). Names are lowercased and stripped of accents and punctuation (`S.A.` → `sa`, `AT&T` → `att`). Then trailing legal suffixes are removed (`inc, llc, ltd, corp, co, gmbh, sa, s.a. de c.v., plc, ag, …`) and the rest is split into tokens. A suffix is never removed if it is the whole name. Hosts lose `www.` and the TLD, and country second levels such as `.co.uk` and `.com.mx` are handled. The remaining label (e.g. `acme-tools`) is split on hyphens.
2. **Domain match**: 1.0 when the concatenated name tokens equal the domain label (`MyCompany` → `mycompany` = `mycompany.com`). An acronym of a multi-word name also counts (`International Business Machines` → `ibm`). A domain match short-circuits the score to **1.0**.
3. **Token overlap**: the average of two values:
   - *containment*: the share of name tokens found in the label. Exact tokens and substrings count (`acmetools` contains `acme`), and so do near-matches with Jaro-Winkler ≥ 0.88.
   - *coverage*: the share of the label explained by the matched tokens.
4. **Fuzzy similarity**: Jaro-Winkler between the concatenated name and the label. It tolerates typos and shared prefixes (`microsft` vs `microsoft` = 0.98).
5. **Offline score** = `1.0` on a domain match, otherwise **`0.6 × tokenOverlap + 0.4 × fuzzy`**. Token overlap carries more weight because it is the stronger evidence. Fuzzy similarity catches typos that overlap misses.
6. **Optional content inspection** (`Relevance:EnableContentInspection`, default **false**). `HttpWebsiteContentFetcher` reads at most `ContentMaxBytes` (256 KB) within `ContentTimeoutSeconds` (3 s). It extracts `<title>`, `og:site_name` and `<meta name="description">`, then measures the share of name tokens present (the ratio). The final score is **`offline + 0.4 × ratio × (1 − offline)`**, so content can only raise the score. A fetch failure or timeout logs a warning and keeps the offline score.

The **same model ranks `q` searches**: `ScoreQuery` scores the query tokens against the company name and against its domain label, and keeps the higher of the two.

| Example | Score |
|---|---|
| MyCompany / www.mycompany.com | 1.00 (domain match) |
| Acme Tools Inc. / acmetools.com | 1.00 (concatenated tokens = label) |
| Café Niño S.A. de C.V. / cafenino.com.mx | 1.00 |
| Microsft / microsoft.com | 0.96 |
| MyCompany / google.com | 0.17 → rejected |
| Banana Co / microsoft.com | 0.00 → rejected |

**Trade-offs.**
- The model is explainable, deterministic, fast, and needs no network.
- It is purely lexical: brands whose domain has nothing in common with the legal name (e.g. *Alphabet Inc.* / google.com) are rejected unless content inspection is enabled and the page mentions the name.
- Short tokens inside long labels can produce false positives (*Apple* / pineapple.com passes).
- Content inspection adds latency and an outbound request driven by user input (an SSRF surface). That is why it is off by default; before enabling it in production, add an allow/deny list for private IP ranges.

## Design decisions

- **Layering.** The Api depends on Application and Infrastructure; Infrastructure depends only on Application's interfaces. Controllers are thin: they map the DTO to a command, call the service, and turn the `Result<T>` into an HTTP result. Business rules live in `CompanyService`, `CompanyInputValidator` and the relevance strategy.
- **Expected failures are values.** The service returns `Result<T>` with status `Ok`, `Invalid` or `NotFound` instead of throwing. Exceptions are reserved for unexpected faults, which `GlobalExceptionHandler` (`IExceptionHandler`) turns into 500s.
- **DTOs only at the boundary.** The API contracts are `CreateCompanyRequest`, `CompanyResponse`, `CompanySearchResult`, `CompanySearchRequest` and `ErrorResponse`. The internal `Company` record also has `Host` and `CreatedAtUtc`, which never appear in a response; an integration test checks this. Mapping lives in `Mapping/CompanyMappings.cs`.
- **One error shape.** `[ApiController]` model-state errors are routed through `ValidationErrorFactory` (`InvalidModelStateResponseFactory`), so data-annotation failures, malformed JSON, service validation and relevance errors all return the same `ErrorResponse`.
- **Repository abstraction.** `ICompanyRepository` is fully async with `CancellationToken`, and its implementations are private to Infrastructure.
  - **Default, `InMemoryCompanyRepository`:** a private `ConcurrentDictionary<Guid, Company>` registered as a singleton. It returns snapshot copies of immutable records, so callers can never change what is stored. This follows the exercise's "in memory only, no database" rule literally.
  - **Optional, `EfCompanyRepository`** (`Persistence:Provider=EfInMemory`): EF Core with its InMemory provider, scoped (one `DbContext` per request), with `AsNoTracking` reads. Each app instance gets its own `InMemoryDatabaseRoot`, so separate hosts such as the integration tests never share data. It shows the path to a real database.
  - Both run the same contract tests (`CompanyRepositoryContractTests`), which is how any future implementation should be checked too.
- **Configuration.** `RelevanceOptions` and `AuthOptions` are bound from `appsettings.json` and validated at startup; an unknown `Persistence:Provider` also fails at startup with a clear message.
- **Non-GUID ids return 400**, not 404. The id has an invalid format, which is a client error. A valid GUID that matches nothing returns 404.
- **Logging.** Information on create, get, search and not-found; Warning on validation, relevance rejection, failed sign-in and content-inspection failure; Error on unhandled exceptions.

## Extensibility

- **PUT/DELETE.** `ICompanyRepository` already has `UpdateAsync` and `DeleteAsync`, implemented and tested in both repositories. To expose them, add `UpdateAsync` to the service (reusing the validator and relevance check, like create) and `DeleteAsync`, then add two controller actions returning 200/204/400/404. The service already produces every `Result` shape needed.
- **Pagination and sorting.** `page` and `pageSize` work today. To add `sortBy` and `sortDir`, add optional properties to `CompanySearchRequest` and `CompanySearchQuery`. The response stays a plain array and the total travels in `X-Total-Count`, so existing clients keep working.
- **Real database.** The EF Core path already exists, so only the provider changes:
  1. Add the provider package (e.g. `Microsoft.EntityFrameworkCore.SqlServer` or `Npgsql.EntityFrameworkCore.PostgreSQL`).
  2. In `AddInfrastructure`, add a provider option that calls `UseSqlServer(connectionString)` or `UseNpgsql(...)` instead of `UseInMemoryDatabase(...)`.
  3. Add `entity.ToTable("Companies")` in `CompanyDbContext` and create the schema: `dotnet ef migrations add InitialCreate -p src/CompanyApi.Infrastructure -s src/CompanyApi.Api`.

  Service, controllers, DTOs and frontend stay the same. For large data sets, also move the name/domain filters and paging from `CompanyService.SearchAsync` into a repository query, so they run in SQL.
- **ML or embedding relevance.** Register another `IRelevanceStrategy`, for example cosine similarity of name and page-text embeddings, or a composite of the current lexical strategy and a semantic one. The contract (`Score` in [0, 1] plus an `Explanation`) and the threshold stay the same.

## Assumptions

- **.NET 10** is used because it is the current LTS; .NET 8 left support on 2026-11-10.
- **Angular 22** is used, with its default test runner (Vitest), instead of Karma.
- "At least a few characters" is interpreted as a minimum of **2** (`CompanyInputValidator.NameMinLength`, one constant to change).
- The website is stored exactly as entered (trimmed), so `https://www.mycompany.com` doesn't come back as `https://www.mycompany.com/`.
- The default `GET /api/companies` order is by name. `q` results are ordered by score.
- HTTPS redirection is off, so the Angular dev server and curl can use `http://localhost:5000` without a certificate prompt.
- FluentAssertions is pinned to 7.x, the last Apache-2.0 release; 8.x needs a commercial license.
