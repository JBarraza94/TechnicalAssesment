import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiError, Company } from './company.models';
import { CompanyListComponent, SEARCH_DEBOUNCE_MS } from './company-list.component';
import { CompanyService, NETWORK_ERROR_MESSAGE } from './company.service';

describe('CompanyListComponent', () => {
  let fixture: ComponentFixture<CompanyListComponent>;
  const serviceMock = { list: vi.fn(), create: vi.fn() };
  const companies: Company[] = [
    { id: '1', name: 'MyCompany', website: 'https://www.mycompany.com' },
    { id: '2', name: 'Acme Tools Inc.', website: 'https://acmetools.com' },
  ];

  const el = (selector: string) => fixture.nativeElement.querySelector(selector) as HTMLElement | null;

  async function render() {
    fixture = TestBed.createComponent(CompanyListComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    serviceMock.list.mockReset();
    await TestBed.configureTestingModule({
      imports: [CompanyListComponent],
      providers: [{ provide: CompanyService, useValue: serviceMock }],
    }).compileComponents();
  });

  afterEach(() => vi.useRealTimers());

  it('init_EmptyList_ShowsEmptyStateMessage', async () => {
    serviceMock.list.mockReturnValue(of([]));

    await render();

    expect(el('[data-testid="empty"]')?.textContent).toContain('No companies found');
    expect(el('table')).toBeNull();
  });

  it('init_WithCompanies_RendersRows', async () => {
    serviceMock.list.mockReturnValue(of(companies));

    await render();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('MyCompany');
    expect(rows[0].textContent).toContain('https://www.mycompany.com');
    expect(el('[data-testid="empty"]')).toBeNull();
  });

  it('init_ServiceFails_ShowsErrorMessage', async () => {
    serviceMock.list.mockReturnValue(throwError(() => new ApiError(NETWORK_ERROR_MESSAGE, 0)));

    await render();

    expect(el('[data-testid="list-error"]')?.textContent).toContain(NETWORK_ERROR_MESSAGE);
  });

  it('refresh_Clicked_ReloadsList', async () => {
    serviceMock.list.mockReturnValueOnce(of([])).mockReturnValueOnce(of(companies));
    await render();

    (el('button') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(serviceMock.list).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('search_Typing_CallsServiceOnceAfterDebounceWithQuery', async () => {
    vi.useFakeTimers();
    serviceMock.list.mockReturnValue(of([]));
    await render();
    serviceMock.list.mockClear();

    const input = el('input[type="search"]') as HTMLInputElement;
    for (const value of ['v', 've', 'mycompany']) {
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    expect(serviceMock.list).not.toHaveBeenCalled();

    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);

    expect(serviceMock.list).toHaveBeenCalledOnce();
    expect(serviceMock.list).toHaveBeenCalledWith('mycompany');
  });
});
