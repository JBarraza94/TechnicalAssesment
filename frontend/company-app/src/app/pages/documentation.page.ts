import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { API_BASE_URL } from '../companies/company.service';
import { NAME_MAX_LENGTH, NAME_MIN_LENGTH } from '../companies/company.validators';

interface EndpointResponse {
  status: string;
  when: string;
  body: string;
}

interface Endpoint {
  method: 'GET' | 'POST';
  route: string;
  purpose: string;
  success: string;
  errors: string;
  curl: string;
  responses: EndpointResponse[];
}

interface Payload {
  title: string;
  description: string;
  status?: string;
  body: string;
}

interface Rule {
  rule: string;
  message: string;
}

interface Example {
  input: string;
  outcome: string;
  valid: boolean;
}

interface RelevanceExample {
  name: string;
  website: string;
  score: number;
  note: string;
}

const json = (value: unknown) => JSON.stringify(value, null, 2);

const AUTH_REQUIRED = "Authentication is required. Sign in and send the token as 'Authorization: Bearer {token}'.";
const AUTH_DISABLED = 'Authentication is disabled on this server (Auth:Enabled = false).';
const TOO_MANY_ATTEMPTS = 'Too many sign-in attempts. Please wait a minute and try again.';

const UNAUTHORIZED: EndpointResponse = {
  status: '401 Unauthorized',
  when: 'Only when authentication is enabled: no token, or the token is invalid or expired.',
  body: json({ message: AUTH_REQUIRED, errors: [AUTH_REQUIRED] }),
};

/** Static reference for the API: payloads, validation rules and the relevance model. Mirrors the backend. */
@Component({
  selector: 'app-documentation-page',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <h1>Documentation</h1>
    </header>

    <nav class="toc" aria-label="On this page">
      <a href="#endpoints" (click)="scrollTo($event, 'endpoints')">Endpoints</a>
      <a href="#payloads" (click)="scrollTo($event, 'payloads')">Payloads</a>
      <a href="#validations" (click)="scrollTo($event, 'validations')">Validations</a>
      <a href="#relevance" (click)="scrollTo($event, 'relevance')">Relevance</a>
    </nav>

    <!-- ---------- Endpoints ---------- -->
    <section id="endpoints" data-testid="docs-endpoints">
      <h2>Endpoints</h2>
      <p>Base URL: <code>{{ baseUrl }}</code></p>
      <p class="alert info" data-testid="docs-auth-note">
        Authentication is <strong>off by default</strong> (<code>Auth:Enabled = false</code>), so the company
        endpoints can be called directly with curl or Postman. When it is enabled, every <code>/api/companies</code>
        endpoint requires a bearer token: get one from <code>POST /api/auth/login</code> and send it as
        <code>Authorization: Bearer &lt;token&gt;</code>; a missing, invalid or expired token gets <code>401</code>.
        <code>GET /api/auth/config</code> tells clients which mode the server is in.
      </p>
      <div class="table-wrap">
        <table>
          <thead>
            <tr><th>Method</th><th>Route</th><th>Purpose</th><th>Success</th><th>Errors</th></tr>
          </thead>
          <tbody>
            @for (e of endpoints; track e.method + e.route) {
              <tr>
                <td><span class="badge" [class.post]="e.method === 'POST'">{{ e.method }}</span></td>
                <td><code>{{ e.route }}</code></td>
                <td>{{ e.purpose }}</td>
                <td>{{ e.success }}</td>
                <td>{{ e.errors }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      @for (e of endpoints; track e.method + e.route) {
        <article class="endpoint" [attr.data-testid]="'endpoint-' + e.method + ' ' + e.route">
          <h3>
            <span class="badge" [class.post]="e.method === 'POST'">{{ e.method }}</span>
            <code>{{ e.route }}</code>
          </h3>
          <p>{{ e.purpose }}.</p>
          <h4>Example request</h4>
          <pre><code>{{ e.curl }}</code></pre>
          <h4>Responses</h4>
          @for (r of e.responses; track r.status + r.when) {
            <div class="response">
              <p>
                <span class="status" [class.bad]="!r.status.startsWith('2')">{{ r.status }}</span>
                {{ r.when }}
              </p>
              <pre><code>{{ r.body }}</code></pre>
            </div>
          }
        </article>
      }

      <h3>Query parameters for <code>GET /api/companies</code></h3>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Parameter</th><th>Description</th></tr></thead>
          <tbody>
            @for (p of queryParams; track p[0]) {
              <tr><td><code>{{ p[0] }}</code></td><td>{{ p[1] }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>

    <!-- ---------- Payloads ---------- -->
    <section id="payloads" data-testid="docs-payloads">
      <h2>Payloads</h2>
      @for (p of payloads; track p.title) {
        <article class="payload">
          <h3>
            {{ p.title }}
            @if (p.status) { <span class="status">{{ p.status }}</span> }
          </h3>
          <p>{{ p.description }}</p>
          <pre><code>{{ p.body }}</code></pre>
        </article>
      }
    </section>

    <!-- ---------- Validations ---------- -->
    <section id="validations" data-testid="docs-validations">
      <h2>Validations</h2>
      <p>
        Rules run on the server (authoritative) and are mirrored by the form on the client.
        The first error becomes <code>message</code>; all of them are listed in <code>errors</code>.
      </p>

      <h3>Company name</h3>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Rule</th><th>Error message</th></tr></thead>
          <tbody>
            @for (r of nameRules; track r.rule) {
              <tr><td>{{ r.rule }}</td><td><code>{{ r.message }}</code></td></tr>
            }
          </tbody>
        </table>
      </div>

      <h3>Website URL</h3>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Rule</th><th>Error message</th></tr></thead>
          <tbody>
            @for (r of websiteRules; track r.rule) {
              <tr><td>{{ r.rule }}</td><td><code>{{ r.message }}</code></td></tr>
            }
          </tbody>
        </table>
      </div>

      <h3>Website examples</h3>
      <ul class="examples">
        @for (ex of websiteExamples; track ex.input) {
          <li [class.ok]="ex.valid" [class.bad]="!ex.valid">
            <span aria-hidden="true">{{ ex.valid ? '✔' : '✘' }}</span>
            <code>{{ ex.input }}</code> — {{ ex.outcome }}
          </li>
        }
      </ul>

      <h3>Request-level errors</h3>
      <ul>
        <li>Empty body or invalid JSON → <code>400</code> <code>The request body is empty or is not valid JSON.</code></li>
        <li><code>{{ '{id}' }}</code> that is not a GUID → <code>400</code>; a valid GUID that does not exist → <code>404</code>.</li>
        <li><code>pageSize</code> outside 1–100 or <code>page</code> &lt; 1 → <code>400</code>.</li>
        <li>Authentication enabled and the bearer token is missing, invalid or expired → <code>401</code>.</li>
        <li>Unexpected server failure → <code>500</code> with a generic message (details are only logged).</li>
      </ul>
    </section>

    <!-- ---------- Relevance ---------- -->
    <section id="relevance" data-testid="docs-relevance">
      <h2>Relevance</h2>
      <p>
        A company is only accepted when its name is relevant to its website. The score is between
        0 and 1; it must be at least <strong>{{ threshold | number: '1.1-1' }}</strong>
        (<code>Relevance:Threshold</code>). The same model ranks <code>?q=</code> searches; results scoring
        below <strong>{{ searchMinScore | number: '1.1-1' }}</strong> (<code>Relevance:SearchMinScore</code>) are hidden.
      </p>

      <h3>How the score is computed</h3>
      <ol class="steps">
        <li>
          <strong>Normalize.</strong> Lowercase, strip accents and punctuation, and drop trailing legal
          suffixes (<code>inc, llc, ltd, corp, co, gmbh, sa, s.a. de c.v., plc, ag…</code>).
          The host loses <code>www.</code> and its TLD (<code>.co.uk</code> and <code>.com.mx</code> included):
          <code>https://www.acme-tools.co.uk</code> → <code>acme-tools</code>.
        </li>
        <li>
          <strong>Domain match.</strong> The joined name tokens equal the domain label
          (<code>MyCompany</code> → <code>mycompany</code> = <code>mycompany.com</code>), or the name's acronym does
          (<code>International Business Machines</code> → <code>ibm</code>). Score is <strong>1.0</strong>.
        </li>
        <li>
          <strong>Token overlap.</strong> Average of <em>containment</em> (share of name tokens found in the
          domain, substrings and near-typos included) and <em>coverage</em> (share of the domain explained by them).
        </li>
        <li>
          <strong>Fuzzy similarity.</strong> Jaro-Winkler between the joined name and the domain label;
          tolerates typos such as <code>Microsft</code> vs <code>microsoft</code>.
        </li>
        <li>
          <strong>Content inspection</strong> (optional, off by default). The page title, <code>og:site_name</code>
          and meta description can only raise the score; if the site is unreachable the offline score is kept.
        </li>
      </ol>

      <h3>Weights</h3>
      <pre class="formula"><code>{{ formula }}</code></pre>

      <h3>Examples</h3>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Name</th><th>Website</th><th>Score</th><th>Result</th></tr></thead>
          <tbody>
            @for (ex of relevanceExamples; track ex.name + ex.website) {
              <tr>
                <td>{{ ex.name }}</td>
                <td><code>{{ ex.website }}</code></td>
                <td>{{ ex.score | number: '1.2-2' }}</td>
                <td [class.ok]="ex.score >= threshold" [class.bad]="ex.score < threshold">
                  {{ ex.score >= threshold ? 'Accepted' : 'Rejected' }} — {{ ex.note }}
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <h3>Rejection payload</h3>
      <p>The explanation is returned so the user can see why the pair was rejected.</p>
      <pre><code>{{ relevanceError }}</code></pre>
    </section>
  `,
})
export class DocumentationPage {
  protected readonly baseUrl = inject(API_BASE_URL);
  protected readonly threshold = 0.5;
  protected readonly searchMinScore = 0.3;

  protected readonly endpoints: Endpoint[] = [
    {
      method: 'GET',
      route: '/api/auth/config',
      purpose: 'Tell clients whether the company endpoints require a bearer token (public)',
      success: '200',
      errors: '—',
      curl: `curl ${this.baseUrl}/auth/config`,
      responses: [{ status: '200 OK', when: 'Always. "enabled" mirrors Auth:Enabled.', body: json({ enabled: false }) }],
    },
    {
      method: 'POST',
      route: '/api/auth/login',
      purpose: 'Sign in and get a bearer token (public; at most 10 attempts per minute)',
      success: '200 + token',
      errors: '400 missing fields, 401 wrong credentials, 429 too many attempts',
      curl: [
        `curl -X POST ${this.baseUrl}/auth/login \\`,
        `  -H "Content-Type: application/json" \\`,
        `  -d '${JSON.stringify({ username: 'admin', password: '<password>' })}'`,
        '',
        '# Keep the token for the next calls:',
        'TOKEN=<accessToken from the response>',
      ].join('\n'),
      responses: [
        {
          status: '200 OK',
          when: 'Valid credentials. The token expires after 60 minutes.',
          body: json({
            accessToken: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9…',
            tokenType: 'Bearer',
            expiresAt: '2026-11-14T02:00:00+00:00',
            username: 'admin',
          }),
        },
        {
          status: '401 Unauthorized',
          when: 'Wrong username or password (the message does not say which).',
          body: json({ message: 'Invalid username or password.', errors: ['Invalid username or password.'] }),
        },
        {
          status: '404 Not Found',
          when: 'Authentication is disabled on this server (Auth:Enabled = false).',
          body: json({ message: AUTH_DISABLED, errors: [AUTH_DISABLED] }),
        },
        {
          status: '429 Too Many Requests',
          when: 'More than 10 sign-in attempts in a minute from the same address.',
          body: json({ message: TOO_MANY_ATTEMPTS, errors: [TOO_MANY_ATTEMPTS] }),
        },
      ],
    },
    {
      method: 'GET',
      route: '/api/auth/me',
      purpose: 'Return the user the token belongs to',
      success: '200',
      errors: '401 missing or invalid token',
      curl: `curl -H "Authorization: Bearer $TOKEN" ${this.baseUrl}/auth/me`,
      responses: [{ status: '200 OK', when: 'Valid token.', body: json({ username: 'admin' }) }, UNAUTHORIZED],
    },
    {
      method: 'POST',
      route: '/api/companies',
      purpose: 'Create a company after validating the name, the website and their relevance',
      success: '201 + Location header',
      errors: '400 validation / relevance / malformed body',
      curl: [
        `curl -i -X POST ${this.baseUrl}/companies \\`,
        `  -H "Content-Type: application/json" \\`,
        `  -d '${JSON.stringify({ name: 'MyCompany', website: 'https://www.mycompany.com' })}'`,
      ].join('\n'),
      responses: [
        {
          status: '201 Created',
          when: `Valid and relevant. Header: Location: ${this.baseUrl}/companies/5f8aa2db-5162-482b-a0ad-19b3f5d98456`,
          body: json({ id: '5f8aa2db-5162-482b-a0ad-19b3f5d98456', name: 'MyCompany', website: 'https://www.mycompany.com' }),
        },
        {
          status: '400 Bad Request',
          when: 'Invalid field (e.g. empty name).',
          body: json({ message: 'Company name is required.', errors: ['Company name is required.'] }),
        },
        {
          status: '400 Bad Request',
          when: 'Name not relevant to the website (e.g. MyCompany / https://www.google.com).',
          body: json({
            message: "Company name 'MyCompany' does not appear to be relevant to website 'https://www.google.com'.",
            errors: ["Company name 'MyCompany' does not appear to be relevant to website 'https://www.google.com'.", 'Name tokens [mycompany] vs domain …'],
          }),
        },
        {
          status: '400 Bad Request',
          when: 'Empty body or invalid JSON.',
          body: json({ message: 'The request body is empty or is not valid JSON.', errors: ['The request body is empty or is not valid JSON.'] }),
        },
        UNAUTHORIZED,
      ],
    },
    {
      method: 'GET',
      route: '/api/companies',
      purpose: 'List every company, filter by name or domain, or rank by relevance with q',
      success: '200 (empty array if none)',
      errors: '400 invalid query',
      curl: [
        `curl ${this.baseUrl}/companies`,
        `curl "${this.baseUrl}/companies?name=comp"`,
        `curl "${this.baseUrl}/companies?domain=mycompany.com"`,
        `curl "${this.baseUrl}/companies?q=acme"`,
        `curl "${this.baseUrl}/companies?page=1&pageSize=10"`,
      ].join('\n'),
      responses: [
        {
          status: '200 OK',
          when: 'Matches found (ordered by name; by score when q is used).',
          body: json([{ id: '5f8aa2db-5162-482b-a0ad-19b3f5d98456', name: 'MyCompany', website: 'https://www.mycompany.com' }]),
        },
        { status: '200 OK', when: 'Nothing matches, or the store is empty.', body: '[]' },
        {
          status: '400 Bad Request',
          when: 'Invalid query, e.g. ?pageSize=0 or ?page=abc.',
          body: json({ message: 'pageSize must be between 1 and 100.', errors: ['pageSize must be between 1 and 100.'] }),
        },
        UNAUTHORIZED,
      ],
    },
    {
      method: 'GET',
      route: '/api/companies/{id}',
      purpose: 'Get one company by its GUID',
      success: '200',
      errors: '400 id is not a GUID, 404 not found',
      curl: `curl ${this.baseUrl}/companies/5f8aa2db-5162-482b-a0ad-19b3f5d98456`,
      responses: [
        {
          status: '200 OK',
          when: 'The company exists.',
          body: json({ id: '5f8aa2db-5162-482b-a0ad-19b3f5d98456', name: 'MyCompany', website: 'https://www.mycompany.com' }),
        },
        {
          status: '404 Not Found',
          when: 'Valid GUID that matches no company.',
          body: json({
            message: "Company '00000000-0000-0000-0000-000000000001' was not found.",
            errors: ["Company '00000000-0000-0000-0000-000000000001' was not found."],
          }),
        },
        {
          status: '400 Bad Request',
          when: 'The id is not a GUID (e.g. /api/companies/not-a-guid).',
          body: json({
            message: "'not-a-guid' is not a valid company id (expected a GUID).",
            errors: ["'not-a-guid' is not a valid company id (expected a GUID)."],
          }),
        },
        UNAUTHORIZED,
      ],
    },
  ];

  protected readonly queryParams: [string, string][] = [
    ['name', 'Partial match on the company name, ignoring case and accents (comp → MyCompany).'],
    ['domain', 'Matches the website host, ignoring www. (mycompany.com, www.mycompany.com or a full URL).'],
    ['q', 'Relevance search across name and domain, ordered by score. Adds score and explanation to each item.'],
    ['page', 'Optional 1-based page number (used with pageSize).'],
    ['pageSize', 'Optional page size, 1–100. The X-Total-Count header returns the total number of matches.'],
  ];

  protected readonly payloads: Payload[] = [
    {
      title: 'Create company — request',
      description: 'POST /api/companies with Content-Type: application/json.',
      body: json({ name: 'MyCompany', website: 'https://www.mycompany.com' }),
    },
    {
      title: 'Create company — response',
      status: '201 Created',
      description: 'The Location header points to /api/companies/{id}. The same shape is returned by GET /api/companies/{id}.',
      body: json({ id: '5f8aa2db-5162-482b-a0ad-19b3f5d98456', name: 'MyCompany', website: 'https://www.mycompany.com' }),
    },
    {
      title: 'List / filter — response',
      status: '200 OK',
      description: 'GET /api/companies, ?name= or ?domain=. Always an array; [] when nothing matches.',
      body: json([
        { id: 'f4d4f58a-14bb-4f1b-ab86-186275ada309', name: 'Acme Tools Inc.', website: 'https://acmetools.com' },
        { id: '5f8aa2db-5162-482b-a0ad-19b3f5d98456', name: 'MyCompany', website: 'https://www.mycompany.com' },
      ]),
    },
    {
      title: 'Relevance search — response',
      status: '200 OK',
      description: 'GET /api/companies?q=acme. Items are ordered by score (highest first) and include an explanation.',
      body: json([
        {
          id: 'f4d4f58a-14bb-4f1b-ab86-186275ada309',
          name: 'Acme Tools Inc.',
          website: 'https://acmetools.com',
          score: 0.7889,
          explanation: 'Query matched name: domain match 0.00, token overlap 0.72, fuzzy similarity 0.89 -> offline score 0.79.',
        },
      ]),
    },
    {
      title: 'Error response',
      status: '400 / 404 / 500',
      description: 'Every error uses the same shape: a short message plus the full list of errors.',
      body: json({ message: `Company name must be at least ${NAME_MIN_LENGTH} characters.`, errors: [`Company name must be at least ${NAME_MIN_LENGTH} characters.`] }),
    },
  ];

  protected readonly nameRules: Rule[] = [
    { rule: 'Required; whitespace-only counts as empty', message: 'Company name is required.' },
    { rule: 'Leading/trailing spaces are trimmed before checking length', message: '—' },
    { rule: `At least ${NAME_MIN_LENGTH} characters`, message: `Company name must be at least ${NAME_MIN_LENGTH} characters.` },
    { rule: `At most ${NAME_MAX_LENGTH} characters`, message: `Company name must be at most ${NAME_MAX_LENGTH} characters.` },
    { rule: 'Must be relevant to the website (see Relevance)', message: "Company name 'X' does not appear to be relevant to website 'Y'." },
  ];

  protected readonly websiteRules: Rule[] = [
    { rule: 'Required', message: 'Website URL is required.' },
    { rule: 'Absolute and well-formed', message: 'Website URL must be an absolute, well-formed URL (e.g. https://www.example.com).' },
    { rule: 'Scheme is http or https', message: 'Website URL scheme must be http or https.' },
    { rule: 'Host contains a dot', message: 'Website URL host must contain a dot (e.g. example.com).' },
  ];

  protected readonly websiteExamples: Example[] = [
    { input: 'https://www.example.com', outcome: 'valid', valid: true },
    { input: 'http://acmetools.com/about', outcome: 'valid (paths and query strings are allowed)', valid: true },
    { input: 'example.com', outcome: 'missing scheme, not an absolute URL', valid: false },
    { input: 'not a url', outcome: 'not a URL', valid: false },
    { input: 'ftp://example.com', outcome: 'scheme must be http or https', valid: false },
    { input: 'https://localhost', outcome: 'host has no dot', valid: false },
  ];

  protected readonly formula = [
    'if domain match:  score = 1.0',
    'otherwise:        score = 0.6 × tokenOverlap + 0.4 × fuzzySimilarity',
    '',
    'with content inspection enabled:',
    '                  final = score + 0.4 × contentRatio × (1 − score)',
    '',
    'accepted when final ≥ 0.5',
  ].join('\n');

  protected readonly relevanceExamples: RelevanceExample[] = [
    { name: 'MyCompany', website: 'https://www.mycompany.com', score: 1.0, note: 'exact domain match' },
    { name: 'Acme Tools Inc.', website: 'https://acmetools.com', score: 1.0, note: 'joined tokens equal the domain' },
    { name: 'Café Niño S.A. de C.V.', website: 'https://www.cafenino.com.mx', score: 1.0, note: 'accents and legal suffix removed' },
    { name: 'Microsft', website: 'https://microsoft.com', score: 0.96, note: 'typo caught by fuzzy similarity' },
    { name: 'Alphabet Inc.', website: 'https://google.com', score: 0.21, note: 'no textual link between name and domain' },
    { name: 'MyCompany', website: 'https://www.google.com', score: 0.17, note: 'only a few shared letters' },
    { name: 'Banana Co', website: 'https://www.microsoft.com', score: 0.0, note: 'nothing in common' },
  ];

  protected readonly relevanceError = json({
    message: "Company name 'MyCompany' does not appear to be relevant to website 'https://www.google.com'.",
    errors: [
      "Company name 'MyCompany' does not appear to be relevant to website 'https://www.google.com'.",
      "Name tokens [mycompany] vs domain 'google': domain match 0.00, token overlap 0.00, fuzzy similarity 0.43 -> offline score 0.17.",
    ],
  });

  /** In-page links: scroll without changing the route (a bare #fragment would hit the router). */
  protected scrollTo(event: Event, id: string): void {
    event.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
