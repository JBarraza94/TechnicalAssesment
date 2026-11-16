import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError, Company } from './company.models';
import { API_BASE_URL, CompanyService, NETWORK_ERROR_MESSAGE, SERVER_ERROR_MESSAGE } from './company.service';

describe('CompanyService', () => {
  const baseUrl = 'http://api.test/api';
  let service: CompanyService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: baseUrl }],
    });
    service = TestBed.inject(CompanyService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('create_ValidRequest_PostsAndReturnsCreatedCompany', async () => {
    const created: Company = { id: '1', name: 'Verus LLC', website: 'https://www.verus.com' };

    const result = firstValueFrom(service.create({ name: 'Verus LLC', website: 'https://www.verus.com' }));
    const req = http.expectOne(`${baseUrl}/companies`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Verus LLC', website: 'https://www.verus.com' });
    req.flush(created, { status: 201, statusText: 'Created' });

    await expect(result).resolves.toEqual(created);
  });

  it('create_ServerReturns400_PropagatesServerMessage', async () => {
    const result = firstValueFrom(service.create({ name: 'Verus LLC', website: 'https://www.google.com' }));
    http.expectOne(`${baseUrl}/companies`).flush(
      {
        message: "Company name 'Verus LLC' does not appear to be relevant to website 'https://www.google.com'.",
        errors: ['…', 'explanation'],
      },
      { status: 400, statusText: 'Bad Request' },
    );

    const error = await result.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).message).toContain('does not appear to be relevant');
    expect((error as ApiError).errors).toContain('explanation');
  });

  it('create_ServerReturns500_UsesFriendlyMessage', async () => {
    const result = firstValueFrom(service.create({ name: 'Verus', website: 'https://verus.com' }));
    http.expectOne(`${baseUrl}/companies`).flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });

    await expect(result).rejects.toMatchObject({ message: SERVER_ERROR_MESSAGE, status: 500 });
  });

  it('list_NetworkError_UsesFriendlyMessage', async () => {
    const result = firstValueFrom(service.list());
    http.expectOne(`${baseUrl}/companies`).error(new ProgressEvent('error'), { status: 0 });

    await expect(result).rejects.toMatchObject({ message: NETWORK_ERROR_MESSAGE, status: 0 });
  });

  it('list_WithQuery_SendsTrimmedQParameter', async () => {
    const result = firstValueFrom(service.list('  verus '));
    const req = http.expectOne((r) => r.url === `${baseUrl}/companies`);
    expect(req.request.params.get('q')).toBe('verus');
    req.flush([]);

    await expect(result).resolves.toEqual([]);
  });

  it('list_WithoutQuery_SendsNoParameters', () => {
    service.list('   ').subscribe();
    const req = http.expectOne(`${baseUrl}/companies`);
    expect(req.request.params.keys()).toEqual([]);
    req.flush([]);
  });
});
