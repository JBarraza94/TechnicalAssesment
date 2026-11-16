import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { ApiError, Company } from './company.models';
import { CompanyFormComponent } from './company-form.component';
import { CompanyService, SERVER_ERROR_MESSAGE } from './company.service';

describe('CompanyFormComponent', () => {
  let fixture: ComponentFixture<CompanyFormComponent>;
  let component: CompanyFormComponent;
  let response$: Subject<Company>;
  const serviceMock = { create: vi.fn(), list: vi.fn() };

  const el = (selector: string) => fixture.nativeElement.querySelector(selector) as HTMLElement | null;
  const submitButton = () => el('button[type="submit"]') as HTMLButtonElement;

  async function fill(name: string, website: string) {
    component.form.setValue({ name, website });
    component.form.markAllAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();
  }

  async function submit() {
    (el('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  beforeEach(async () => {
    response$ = new Subject<Company>();
    serviceMock.create.mockReset().mockReturnValue(response$);

    await TestBed.configureTestingModule({
      imports: [CompanyFormComponent],
      providers: [{ provide: CompanyService, useValue: serviceMock }],
    }).compileComponents();

    fixture = TestBed.createComponent(CompanyFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('submitButton_EmptyFields_IsDisabled', () => {
    expect(component.form.invalid).toBe(true);
    expect(submitButton().disabled).toBe(true);
  });

  it.each([
    ['example.com', 'url'],
    ['not a url', 'url'],
    ['ftp://example.com', 'urlScheme'],
    ['https://localhost', 'urlHost'],
  ])('submitButton_MalformedUrl_%s_IsDisabled', async (website, errorKey) => {
    await fill('Verus LLC', website);

    expect(component.form.controls.website.hasError(errorKey)).toBe(true);
    expect(submitButton().disabled).toBe(true);
    expect(el('[data-testid="website-error"]')).not.toBeNull();
  });

  it.each([['A'], ['   ']])('submitButton_InvalidName_%j_IsDisabled', async (name) => {
    await fill(name, 'https://www.verus.com');

    expect(submitButton().disabled).toBe(true);
    expect(el('[data-testid="name-error"]')?.textContent).toMatch(/Company name/);
  });

  it('submit_InvalidForm_DoesNotCallService', async () => {
    await fill('', 'nope');
    component.submit();

    expect(serviceMock.create).not.toHaveBeenCalled();
  });

  it('submit_ValidForm_CallsServiceWithTrimmedValues', async () => {
    await fill('  Verus LLC ', ' https://www.verus.com ');
    expect(submitButton().disabled).toBe(false);

    await submit();

    expect(serviceMock.create).toHaveBeenCalledOnce();
    expect(serviceMock.create).toHaveBeenCalledWith({ name: 'Verus LLC', website: 'https://www.verus.com' });
  });

  it('submit_ServerReturns201_ShowsSuccessAndEmitsCreated', async () => {
    const emitted: Company[] = [];
    component.created.subscribe((c) => emitted.push(c));
    await fill('Verus LLC', 'https://www.verus.com');
    await submit();

    const company: Company = { id: 'abc', name: 'Verus LLC', website: 'https://www.verus.com' };
    response$.next(company);
    response$.complete();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el('[data-testid="success"]')?.textContent).toContain('Verus LLC');
    expect(el('[data-testid="error"]')).toBeNull();
    expect(emitted).toEqual([company]);
    expect(component.form.getRawValue()).toEqual({ name: '', website: '' });
  });

  it('submit_ServerReturns400_ShowsServerMessage', async () => {
    const message = "Company name 'Verus LLC' does not appear to be relevant to website 'https://www.google.com'.";
    await fill('Verus LLC', 'https://www.google.com');
    await submit();

    response$.error(new ApiError(message, 400, [message, 'token overlap 0.00']));
    fixture.detectChanges();
    await fixture.whenStable();

    const error = el('[data-testid="error"]');
    expect(error?.textContent).toContain(message);
    expect(error?.textContent).toContain('token overlap 0.00');
    expect(el('[data-testid="success"]')).toBeNull();
    expect(submitButton().disabled).toBe(false);
  });

  it('submit_UnexpectedError_ShowsFriendlyMessage', async () => {
    await fill('Verus LLC', 'https://www.verus.com');
    await submit();

    response$.error(new Error('kaboom'));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el('[data-testid="error"]')?.textContent).toContain(SERVER_ERROR_MESSAGE);
  });
});
