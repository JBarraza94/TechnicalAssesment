import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../companies/company.models';
import { API_BASE_URL } from '../companies/company.service';
import { AuthService, SESSION_STORAGE_KEY } from './auth.service';
import { fakeSession } from './auth.testing';

describe('AuthService', () => {
  const baseUrl = 'http://api.test/api';
  let http: HttpTestingController;

  function create(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: baseUrl }],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => http?.verify());

  it('login_ValidCredentials_StoresSessionAndIsAuthenticated', async () => {
    const auth = create();
    const session = fakeSession();

    const result = firstValueFrom(auth.login('admin', 'Admin123!'));
    const req = http.expectOne(`${baseUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ username: 'admin', password: 'Admin123!' });
    req.flush(session);

    await expect(result).resolves.toEqual(session);
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.username()).toBe('admin');
    expect(auth.token()).toBe(session.accessToken);
    expect(JSON.parse(sessionStorage.getItem(SESSION_STORAGE_KEY)!)).toEqual(session);
  });

  it('login_WrongCredentials_PropagatesServerMessageAndStaysSignedOut', async () => {
    const auth = create();

    const result = firstValueFrom(auth.login('admin', 'nope'));
    http
      .expectOne(`${baseUrl}/auth/login`)
      .flush({ message: 'Invalid username or password.', errors: [] }, { status: 401, statusText: 'Unauthorized' });

    const error = await result.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).message).toBe('Invalid username or password.');
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('logout_SignedIn_ClearsSessionAndStorage', () => {
    const auth = create();
    auth.setSession(fakeSession());

    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token()).toBeNull();
    expect(sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('isAuthenticated_ExpiredSession_ReturnsFalse', () => {
    const auth = create();

    auth.setSession(fakeSession(-1));

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token()).toBeNull();
  });

  it('constructor_SessionInStorage_RestoresIt', () => {
    sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(fakeSession(30, 'restored')));

    const auth = create();

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.username()).toBe('restored');
  });

  it('authRequired_BeforeLoadConfig_IsTrue', () => {
    expect(create().authRequired()).toBe(true);
  });

  it('loadConfig_ServerSaysDisabled_OpensAppWithoutSignIn', async () => {
    const auth = create();

    const done = auth.loadConfig();
    http.expectOne(`${baseUrl}/auth/config`).flush({ enabled: false });
    await done;

    expect(auth.authRequired()).toBe(false);
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.canAccessApp()).toBe(true);
  });

  it('loadConfig_ServerSaysEnabled_RequiresSignIn', async () => {
    const auth = create();

    const done = auth.loadConfig();
    http.expectOne(`${baseUrl}/auth/config`).flush({ enabled: true });
    await done;

    expect(auth.authRequired()).toBe(true);
    expect(auth.canAccessApp()).toBe(false);
  });

  it('loadConfig_ServerUnreachable_KeepsAuthRequiredWithoutThrowing', async () => {
    const auth = create();

    const done = auth.loadConfig();
    http.expectOne(`${baseUrl}/auth/config`).error(new ProgressEvent('error'), { status: 0 });

    await expect(done).resolves.toBeUndefined();
    expect(auth.authRequired()).toBe(true);
  });

  it('constructor_CorruptStorage_StartsSignedOut', () => {
    sessionStorage.setItem(SESSION_STORAGE_KEY, '{not json');

    expect(create().isAuthenticated()).toBe(false);
  });
});
