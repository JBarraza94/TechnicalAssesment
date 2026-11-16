# Company API (.NET 10) + Angular frontend

A small REST API that stores companies (name + website) in memory. It accepts a company only when its name is *relevant* to its website, using an explainable relevance model. An Angular app lets you create companies, list them and search them.

```
CompanyApi.sln
src/CompanyApi.Api              Controllers, Program.cs, DTOs (Contracts/), error mapping, DI wiring
src/CompanyApi.Application      Domain model, ICompanyService/CompanyService, validator, relevance model, interfaces
src/CompanyApi.Infrastructure   InMemoryCompanyRepository, HttpWebsiteContentFetcher
tests/CompanyApi.UnitTests      Validator, relevance, service, repository, mapping, content-fetcher tests
tests/CompanyApi.IntegrationTests  API tests with WebApplicationFactory
frontend/company-app            Angular 22 app (standalone components, reactive forms, HttpClient)
```

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.x (LTS) | `global.json` pins 10.0 with `latestFeature` roll-forward |
| Node.js | 22 LTS (≥ 20.19 required by Angular 22) | `frontend/company-app/.nvmrc` → `nvm use 22` |
| npm | 10+ | ships with Node 22 |

HTTPS on port 7000 uses the ASP.NET Core dev certificate. Trust it once with `dotnet dev-certs https --trust`, or call the API over plain HTTP on port 5000.

## Run

**Backend:** listens on http://localhost:5000 and https://localhost:7000.

```bash
cd src/CompanyApi.Api
dotnet run
```

In Development, Swagger UI is at http://localhost:5000/swagger and the OpenAPI document is at `/openapi/v1.json`.

**Frontend:** runs at http://localhost:4200.

```bash
cd frontend/company-app
nvm use 22            # or any Node ≥ 20.19
npm install
npx ng serve          # or: npm start
```

The API base URL is set in `src/environments/environment.ts` (`http://localhost:5000/api`) and injected through the `API_BASE_URL` token. The backend enables CORS for `http://localhost:4200` (configurable under `Cors:AllowedOrigins`).

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
curl -i -X POST $B -H "Content-Type: application/json" -d '{"name":"Verus LLC","website":"https://www.verus.com"}'
# HTTP/1.1 201 Created
# Location: http://localhost:5000/api/companies/5f8aa2db-5162-482b-a0ad-19b3f5d98456
# {"id":"5f8aa2db-5162-482b-a0ad-19b3f5d98456","name":"Verus LLC","website":"https://www.verus.com"}

# POST: validation errors (400)
curl -X POST $B -H "Content-Type: application/json" -d '{"name":"","website":"https://www.verus.com"}'
# {"message":"Company name is required.","errors":["Company name is required."]}
curl -X POST $B -H "Content-Type: application/json" -d '{"name":"Verus","website":"ftp://verus.com"}'
# {"message":"Website URL scheme must be http or https.", ...}

# POST: irrelevant name/website pair (400, with the explanation)
curl -X POST $B -H "Content-Type: application/json" -d '{"name":"Verus LLC","website":"https://www.google.com"}'
# {"message":"Company name 'Verus LLC' does not appear to be relevant to website 'https://www.google.com'.",
#  "errors":[..., "Name tokens [verus] vs domain 'google': domain match 0.00, token overlap 0.00, fuzzy similarity 0.00 -> offline score 0.00."]}

# POST: malformed JSON (400)
curl -X POST $B -H "Content-Type: application/json" -d '{"name": "Verus",'
# {"message":"The request body is empty or is not valid JSON.", ...}

# GET list: success, filters, relevance search
curl $B                      # [] when empty, otherwise every company
curl "$B?name=ver"           # [{"id":"…","name":"Verus LLC","website":"https://www.verus.com"}]
curl "$B?domain=verus.com"   # same
curl "$B?q=acme"             # [{"id":"…","name":"Acme Tools Inc.","website":"https://acmetools.com","score":0.7889,"explanation":"…"}]
curl "$B?name=zzz"           # []

# GET list: invalid query (400)
curl "$B?pageSize=0"         # {"message":"pageSize must be between 1 and 100.", ...}

# GET by id: success, not found, bad format
curl $B/5f8aa2db-5162-482b-a0ad-19b3f5d98456                  # 200
curl $B/00000000-0000-0000-0000-000000000001                  # 404 {"message":"Company '…' was not found.", ...}
curl $B/not-a-guid                                            # 400 {"message":"'not-a-guid' is not a valid company id (expected a GUID).", ...}
```

## Validation rules

Both server rules and client rules apply. `CompanyInputValidator` is the authoritative server implementation, and the Angular validators mirror it.

- **Name:** required, not whitespace-only, trimmed, `NameMinLength = 2`, `NameMaxLength = 200`.
- **Website:** required, absolute, well-formed, scheme `http` or `https`, and a host containing a dot. `https://localhost` and `http://example` are rejected.
- **Relevance:** the score must be ≥ `Relevance:Threshold` (default **0.5**, in `appsettings.json`).

## Relevance strategy

The strategy is `IRelevanceStrategy` → `CompositeRelevanceStrategy`. It returns `RelevanceResult(Score, Explanation)`, where the score is in [0, 1]. It is deterministic and works offline by default.

1. **Normalize** (`TextNormalizer`). Names are lowercased and stripped of accents and punctuation (`S.A.` → `sa`, `AT&T` → `att`). Then trailing legal suffixes are removed (`inc, llc, ltd, corp, co, gmbh, sa, s.a. de c.v., plc, ag, …`) and the rest is split into tokens. A suffix is never removed if it is the whole name. Hosts lose `www.` and the TLD, and country second levels such as `.co.uk` and `.com.mx` are handled. The remaining label (e.g. `acme-tools`) is split on hyphens.
2. **Domain match**: 1.0 when the concatenated name tokens equal the domain label (`Verus LLC` → `verus` = `verus.com`). An acronym of a multi-word name also counts (`International Business Machines` → `ibm`). A domain match short-circuits the score to **1.0**.
3. **Token overlap**: the average of two values:
   - *containment*: the share of name tokens found in the label. Exact tokens and substrings count (`acmetools` contains `acme`), and so do near-matches with Jaro-Winkler ≥ 0.88.
   - *coverage*: the share of the label explained by the matched tokens.
4. **Fuzzy similarity**: Jaro-Winkler between the concatenated name and the label. It tolerates typos and shared prefixes (`microsft` vs `microsoft` = 0.98).
5. **Offline score** = `1.0` on a domain match, otherwise **`0.6 × tokenOverlap + 0.4 × fuzzy`**. Token overlap carries more weight because it is the stronger evidence. Fuzzy similarity catches typos that overlap misses.
6. **Optional content inspection** (`Relevance:EnableContentInspection`, default **false**). `HttpWebsiteContentFetcher` reads at most `ContentMaxBytes` (256 KB) within `ContentTimeoutSeconds` (3 s). It extracts `<title>`, `og:site_name` and `<meta name="description">`, then measures the share of name tokens present (the ratio). The final score is **`offline + 0.4 × ratio × (1 − offline)`**, so content can only raise the score. A fetch failure or timeout logs a warning and keeps the offline score.

The **same model ranks `q` searches**: `ScoreQuery` scores the query tokens against the company name and against its domain label, and keeps the higher of the two.

| Example | Score |
|---|---|
| Verus LLC / www.verus.com | 1.00 (domain match) |
| Acme Tools Inc. / acmetools.com | 1.00 (concatenated tokens = label) |
| Café Niño S.A. de C.V. / cafenino.com.mx | 1.00 |
| Microsft / microsoft.com | 0.96 |
| Verus LLC / google.com | 0.00 → rejected |
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
- **Repository abstraction.** `ICompanyRepository` is fully async with `CancellationToken`. `InMemoryCompanyRepository` keeps a private `ConcurrentDictionary<Guid, Company>`, returns snapshot copies of immutable records, and is registered as a singleton in `Infrastructure/DependencyInjection.cs`.
- **Configuration.** `RelevanceOptions` is bound from `appsettings.json` and validated with data annotations at startup.
- **Non-GUID ids return 400**, not 404. The id has an invalid format, which is a client error. A valid GUID that matches nothing returns 404.
- **Logging.** Information on create, get, search and not-found; Warning on validation, relevance rejection and content-inspection failure; Error on unhandled exceptions.

## Extensibility

- **PUT/DELETE.** `ICompanyRepository` already has `UpdateAsync` and `DeleteAsync`, both implemented and tested. To expose them, add `UpdateAsync` to the service (reusing the validator and relevance check, like create) and `DeleteAsync`, then add two controller actions returning 200/204/400/404. The service already produces every `Result` shape needed.
- **Pagination and sorting.** `page` and `pageSize` work today. To add `sortBy` and `sortDir`, add optional properties to `CompanySearchRequest` and `CompanySearchQuery`. The response stays a plain array and the total travels in `X-Total-Count`, so existing clients keep working.
- **Real database.** Implement `ICompanyRepository` with EF Core (move the name and domain filters into the query, add an index on `Host`) and change one line in `AddInfrastructure`. Nothing else changes.
- **ML or embedding relevance.** Register another `IRelevanceStrategy`, for example cosine similarity of name and page-text embeddings, or a composite of the current lexical strategy and a semantic one. The contract (`Score` in [0, 1] plus an `Explanation`) and the threshold stay the same.

## Assumptions

- **.NET 10** is used because it is the current LTS; .NET 8 left support on 2026-11-10.
- **Angular 22** is used, with its default test runner (Vitest), instead of Karma.
- The website is stored exactly as entered (trimmed), so `https://www.verus.com` doesn't come back as `https://www.verus.com/`.
- The default `GET /api/companies` order is by name. `q` results are ordered by score.
- HTTPS redirection is off, so the Angular dev server and curl can use `http://localhost:5000` without a certificate prompt.
- FluentAssertions is pinned to 7.x, the last Apache-2.0 release; 8.x needs a commercial license.
