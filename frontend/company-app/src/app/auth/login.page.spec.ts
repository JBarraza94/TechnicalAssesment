import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Subject, of } from 'rxjs';
import { routes } from '../app.routes';
import { ApiError } from '../companies/company.models';
import { provideHttpClient } from '@angular/common/http';
import { API_BASE_URL, CompanyService } from '../companies/company.service';
import { AuthService, AuthSession } from './auth.service';
import { fakeSession } from './auth.testing';

describe('LoginPage', () => {
  let harness: RouterTestingHarness;
  let response$: Subject<AuthSession>;
  let auth: AuthService;

  const query = (selector: string) => harness.routeNativeElement?.querySelector(selector) as HTMLElement | null;
  const submitButton = () => query('button[type="submit"]') as HTMLButtonElement;

  async function type(selector: string, value: string) {
    const input = query(selector) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    harness.detectChanges();
    await harness.fixture.whenStable();
  }

  async function submit() {
    (query('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    harness.detectChanges();
    await harness.fixture.whenStable();
  }

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
    auth = TestBed.inject(AuthService);
    response$ = new Subject<AuthSession>();
    vi.spyOn(auth, 'login').mockReturnValue(response$);
  });

  it('render_EmptyFields_SubmitIsDisabled', async () => {
    harness = await RouterTestingHarness.create('/login');

    expect(query('h1')?.textContent).toContain('Sign in');
    expect(submitButton().disabled).toBe(true);
  });

  it('submit_ValidCredentials_CallsLoginAndNavigatesToReturnUrl', async () => {
    harness = await RouterTestingHarness.create('/login?returnUrl=%2Fsearch');
    await type('#username', ' admin ');
    await type('#password', 'Admin123!');

    await submit();
    expect(auth.login).toHaveBeenCalledWith('admin', 'Admin123!');

    auth.setSession(fakeSession());
    response$.next(fakeSession());
    response$.complete();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/search');
  });

  it('submit_UnsafeReturnUrl_NavigatesToAddInstead', async () => {
    harness = await RouterTestingHarness.create('/login?returnUrl=https%3A%2F%2Fevil.example');
    await type('#username', 'admin');
    await type('#password', 'Admin123!');

    await submit();
    auth.setSession(fakeSession());
    response$.next(fakeSession());
    response$.complete();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/add');
  });

  it('submit_WrongCredentials_ShowsServerMessageAndClearsPassword', async () => {
    harness = await RouterTestingHarness.create('/login');
    await type('#username', 'admin');
    await type('#password', 'wrong');

    await submit();
    response$.error(new ApiError('Invalid username or password.', 401));
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(query('[data-testid="login-error"]')?.textContent).toContain('Invalid username or password.');
    expect((query('#password') as HTMLInputElement).value).toBe('');
  });

  it('render_ExpiredReason_ShowsExpiredNotice', async () => {
    harness = await RouterTestingHarness.create('/login?reason=expired');

    expect(query('[data-testid="expired"]')).not.toBeNull();
  });

  it('protectedRoute_SignedOut_RedirectsToLogin', async () => {
    harness = await RouterTestingHarness.create('/search');

    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Fsearch');
    expect(query('h1')?.textContent).toContain('Sign in');
  });
});
