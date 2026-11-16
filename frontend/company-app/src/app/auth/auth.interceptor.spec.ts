import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { API_BASE_URL } from '../companies/company.service';
import { authGuard, guestGuard, safeReturnUrl } from './auth.guards';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { signIn } from './auth.testing';

const baseUrl = 'http://api.test/api';

function setup() {
  sessionStorage.clear();
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: baseUrl },
    ],
  });
  return { http: TestBed.inject(HttpClient), backend: TestBed.inject(HttpTestingController) };
}

describe('authInterceptor', () => {
  it('apiRequest_SignedIn_AddsBearerHeader', () => {
    const { http, backend } = setup();
    signIn();

    http.get(`${baseUrl}/companies`).subscribe();

    expect(backend.expectOne(`${baseUrl}/companies`).request.headers.get('Authorization')).toBe('Bearer token-for-admin');
    backend.verify();
  });

  it('apiRequest_SignedOut_SendsNoHeader', () => {
    const { http, backend } = setup();

    http.get(`${baseUrl}/companies`).subscribe();

    expect(backend.expectOne(`${baseUrl}/companies`).request.headers.has('Authorization')).toBe(false);
  });

  it('otherHost_SignedIn_NeverLeaksToken', () => {
    const { http, backend } = setup();
    signIn();

    http.get('https://example.com/data').subscribe();

    expect(backend.expectOne('https://example.com/data').request.headers.has('Authorization')).toBe(false);
  });

  it('apiReturns401_SignedIn_LogsOutAndRedirectsToLogin', () => {
    const { http, backend } = setup();
    signIn();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    http.get(`${baseUrl}/companies`).subscribe({ error: () => undefined });
    backend.expectOne(`${baseUrl}/companies`).flush({ message: 'Authentication is required.' }, { status: 401, statusText: 'Unauthorized' });

    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login'], expect.objectContaining({ queryParams: expect.objectContaining({ reason: 'expired' }) }));
  });

  it('loginReturns401_DoesNotRedirect', () => {
    const { http, backend } = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');

    http.post(`${baseUrl}/auth/login`, {}).subscribe({ error: () => undefined });
    backend.expectOne(`${baseUrl}/auth/login`).flush({ message: 'Invalid username or password.' }, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
  });
});

describe('auth guards', () => {
  const runGuard = (guard: typeof authGuard, url = '/search') =>
    TestBed.runInInjectionContext(() => guard({} as never, { url } as never));

  it('authGuard_SignedOut_RedirectsToLoginWithReturnUrl', () => {
    setup();

    const result = runGuard(authGuard);

    expect(TestBed.inject(Router).serializeUrl(result as never)).toBe('/login?returnUrl=%2Fsearch');
  });

  it('authGuard_SignedIn_AllowsNavigation', () => {
    setup();
    signIn();

    expect(runGuard(authGuard)).toBe(true);
  });

  it('guestGuard_SignedIn_RedirectsToAdd', () => {
    setup();
    signIn();

    expect(TestBed.inject(Router).serializeUrl(runGuard(guestGuard) as never)).toBe('/add');
  });

  it('guestGuard_SignedOut_AllowsLoginPage', () => {
    setup();

    expect(runGuard(guestGuard)).toBe(true);
  });

  it.each([
    ['/search', '/search'],
    ['/search?q=acme', '/search?q=acme'],
    [null, '/add'],
    ['https://evil.example', '/add'],
    ['//evil.example', '/add'],
    ['/\\evil.example', '/add'],
    ['/login', '/add'],
  ])('safeReturnUrl_%s_Returns%s', (input, expected) => {
    expect(safeReturnUrl(input)).toBe(expected);
  });
});
