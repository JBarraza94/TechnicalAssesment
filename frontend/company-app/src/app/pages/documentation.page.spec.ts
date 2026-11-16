import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app.routes';
import { API_BASE_URL } from '../companies/company.service';

describe('DocumentationPage', () => {
  let root: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), { provide: API_BASE_URL, useValue: 'http://localhost:5000/api' }],
    });
    const harness = await RouterTestingHarness.create('/docs');
    root = harness.routeNativeElement!;
  });

  const text = (selector: string) => root.querySelector(selector)?.textContent ?? '';

  it('render_DocsRoute_ShowsAllSections', () => {
    expect(text('h1')).toContain('Documentation');
    for (const id of ['endpoints', 'payloads', 'validations', 'relevance']) {
      expect(root.querySelector(`[data-testid="docs-${id}"]`)).not.toBeNull();
    }
  });

  it('endpoints_Rendered_ListsEveryEndpointWithExampleAndResponses', () => {
    const cards = Array.from(root.querySelectorAll('.endpoint'));
    const titles = cards.map((c) => `${c.querySelector('h3 .badge')?.textContent} ${c.querySelector('h3 code')?.textContent}`);
    expect(titles).toEqual([
      'POST /api/auth/login',
      'GET /api/auth/me',
      'POST /api/companies',
      'GET /api/companies',
      'GET /api/companies/{id}',
    ]);

    const getById = cards[4].textContent ?? '';
    expect(getById).toContain('curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/companies/');
    expect(getById).toContain('401 Unauthorized');
    expect(getById).toContain('200 OK');
    expect(getById).toContain('404 Not Found');
    expect(getById).toContain('400 Bad Request');
  });

  it('authNote_Rendered_ExplainsBearerToken', () => {
    expect(text('[data-testid="docs-auth-note"]')).toContain('Authorization: Bearer');
    expect(text('.endpoint')).toContain('Invalid username or password.');
  });

  it('payloads_Rendered_ShowsCreateRequestJson', () => {
    expect(text('[data-testid="docs-payloads"]')).toContain('"website": "https://www.mycompany.com"');
  });

  it('validations_Rendered_ShowsServerMessages', () => {
    const validations = text('[data-testid="docs-validations"]');
    expect(validations).toContain('Company name must be at least 2 characters.');
    expect(validations).toContain('Website URL scheme must be http or https.');
    expect(validations).toContain('Website URL host must contain a dot (e.g. example.com).');
  });

  it('relevance_Rendered_ShowsWeightsThresholdAndExamples', () => {
    const relevance = text('[data-testid="docs-relevance"]');
    expect(relevance).toContain('0.6 × tokenOverlap + 0.4 × fuzzySimilarity');
    expect(relevance).toContain('0.5');
    expect(relevance).toContain('Microsft');
    expect(relevance).toContain('Rejected');
  });
});
