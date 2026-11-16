import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { App } from './app';
import { routes } from './app.routes';
import { AuthService } from './auth/auth.service';
import { signIn } from './auth/auth.testing';
import { API_BASE_URL, CompanyService } from './companies/company.service';

describe('App', () => {
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        { provide: API_BASE_URL, useValue: 'http://api.test/api' },
        { provide: CompanyService, useValue: { list: () => of([]), create: vi.fn() } },
      ],
    });
  });

  async function render() {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  const linkTexts = (root: HTMLElement) =>
    Array.from(root.querySelectorAll('.sidebar nav a')).map((a) => [a.textContent?.trim(), a.getAttribute('href')]);

  it('sidebar_SignedOut_ShowsDocumentationAndSignInOnly', async () => {
    const { root } = await render();

    expect(linkTexts(root)).toEqual([
      ['Documentation', '/docs'],
      ['Sign in', '/login'],
    ]);
    expect(root.querySelector('[data-testid="user"]')).toBeNull();
  });

  it('sidebar_SignedIn_ShowsAllPagesUserAndLogout', async () => {
    signIn('admin');

    const { root } = await render();

    expect(linkTexts(root)).toEqual([
      ['Add company', '/add'],
      ['Search companies', '/search'],
      ['Documentation', '/docs'],
    ]);
    expect(root.querySelector('[data-testid="user"]')?.textContent).toContain('admin');
  });

  it('logout_Clicked_ClearsSessionAndGoesToLogin', async () => {
    signIn();
    const { fixture, root } = await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    (root.querySelector('[data-testid="user"] button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/login');
    expect(root.querySelector('[data-testid="user"]')).toBeNull();
  });

  it('menuToggle_Clicked_OpensAndClosesMobileMenu', async () => {
    const { fixture, root } = await render();
    const toggle = root.querySelector('.menu-toggle') as HTMLButtonElement;
    const sidebar = root.querySelector('#sidebar') as HTMLElement;

    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    toggle.click();
    fixture.detectChanges();
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(sidebar.classList).toContain('open');

    toggle.click();
    fixture.detectChanges();
    expect(sidebar.classList).not.toContain('open');
  });

  it('navigation_MenuOpen_ClosesMenu', async () => {
    const { fixture, root } = await render();
    fixture.componentInstance.menuOpen.set(true);

    await TestBed.inject(Router).navigateByUrl('/docs');
    fixture.detectChanges();

    expect(root.querySelector('#sidebar')?.classList).not.toContain('open');
  });

  it('navigate_EmptyPathSignedIn_RedirectsToAddPage', async () => {
    signIn();
    const harness = await RouterTestingHarness.create('/');

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toContain('Add company');
  });

  it('navigate_UnknownPathSignedOut_EndsOnLogin', async () => {
    await RouterTestingHarness.create('/does-not-exist');

    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Fadd');
  });
});
