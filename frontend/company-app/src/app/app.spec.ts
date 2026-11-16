import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { App } from './app';
import { CompanyService } from './companies/company.service';

describe('App', () => {
  it('renders the form and the list', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [{ provide: CompanyService, useValue: { list: () => of([]), create: vi.fn() } }],
    }).compileComponents();

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('h1')?.textContent).toContain('Company Directory');
    expect(root.querySelector('app-company-form form')).not.toBeNull();
    expect(root.querySelector('app-company-list')).not.toBeNull();
  });
});
