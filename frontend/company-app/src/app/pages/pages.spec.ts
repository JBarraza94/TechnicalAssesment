import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { routes } from '../app.routes';
import { signIn } from '../auth/auth.testing';
import { API_BASE_URL, CompanyService } from '../companies/company.service';

describe('Pages', () => {
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        { provide: API_BASE_URL, useValue: 'http://api.test/api' },
        { provide: CompanyService, useValue: { list: vi.fn(() => of([])), create: vi.fn() } },
      ],
    });
    signIn();
    harness = await RouterTestingHarness.create();
  });

  const query = (selector: string) => harness.routeNativeElement?.querySelector(selector) as HTMLElement | null;

  async function click(selector: string) {
    query(selector)!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  it('addPage_Rendered_ShowsFormAndSearchCompanyButton', async () => {
    await harness.navigateByUrl('/add');

    expect(query('h1')?.textContent).toContain('Add company');
    expect(query('app-company-form')).not.toBeNull();
    expect(query('[data-testid="go-search"]')?.textContent?.trim()).toBe('Search company');
  });

  it('addPage_SearchCompanyClicked_NavigatesToSearchPage', async () => {
    await harness.navigateByUrl('/add');

    await click('[data-testid="go-search"]');

    expect(TestBed.inject(Router).url).toBe('/search');
    expect(query('app-company-list')).not.toBeNull();
  });

  it('searchPage_Rendered_ShowsListAndAddCompanyButton', async () => {
    await harness.navigateByUrl('/search');

    expect(query('h1')?.textContent).toContain('Search companies');
    expect(query('app-company-list')).not.toBeNull();
    expect(query('[data-testid="go-add"]')?.textContent?.trim()).toBe('Add company');
  });

  it('searchPage_AddCompanyClicked_NavigatesToAddPage', async () => {
    await harness.navigateByUrl('/search');

    await click('[data-testid="go-add"]');

    expect(TestBed.inject(Router).url).toBe('/add');
    expect(query('app-company-form')).not.toBeNull();
  });

  it('loginPage_SignedIn_RedirectsToAdd', async () => {
    await harness.navigateByUrl('/login');

    expect(TestBed.inject(Router).url).toBe('/add');
  });
});
