import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiError, Company } from './company.models';
import { CompanyService, SERVER_ERROR_MESSAGE } from './company.service';
import { NAME_MAX_LENGTH, NAME_MIN_LENGTH, companyNameValidator, httpUrlValidator } from './company.validators';

@Component({
  selector: 'app-company-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <label for="name">Company name</label>
      <input id="name" formControlName="name" autocomplete="organization" placeholder="MyCompany" />
      @if (form.controls.name.touched && form.controls.name.errors; as e) {
        <small class="field-error" data-testid="name-error">
          @if (e['required']) { Company name is required. }
          @else if (e['minlength']) { Company name must be at least {{ minLength }} characters. }
          @else if (e['maxlength']) { Company name must be at most {{ maxLength }} characters. }
        </small>
      }

      <label for="website">Website URL</label>
      <input id="website" formControlName="website" type="url" autocomplete="url" placeholder="https://www.mycompany.com" />
      @if (form.controls.website.touched && form.controls.website.errors; as e) {
        <small class="field-error" data-testid="website-error">
          @if (e['required']) { Website URL is required. }
          @else if (e['urlScheme']) { Website URL must start with http:// or https://. }
          @else if (e['urlHost']) { Website URL host must contain a dot (e.g. example.com). }
          @else { Enter an absolute URL such as https://www.example.com. }
        </small>
      }

      <button type="submit" [disabled]="form.invalid || submitting()">
        {{ submitting() ? 'Saving…' : 'Add company' }}
      </button>

      @if (successMessage(); as msg) {
        <p class="alert success" role="status" data-testid="success">{{ msg }}</p>
      }
      @if (errorMessage(); as msg) {
        <div class="alert error" role="alert" data-testid="error">
          <p>{{ msg }}</p>
          @if (errorDetails().length) {
            <ul>
              @for (detail of errorDetails(); track $index) { <li>{{ detail }}</li> }
            </ul>
          }
        </div>
      }
    </form>
  `,
})
export class CompanyFormComponent {
  private readonly companyService = inject(CompanyService);

  /** Emitted after the API returns 201 so the parent can refresh the list. */
  readonly created = output<Company>();

  protected readonly minLength = NAME_MIN_LENGTH;
  protected readonly maxLength = NAME_MAX_LENGTH;

  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [companyNameValidator] }),
    website: new FormControl('', { nonNullable: true, validators: [httpUrlValidator] }),
  });

  readonly submitting = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly errorDetails = signal<string[]>([]);

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;

    const { name, website } = this.form.getRawValue();
    this.submitting.set(true);
    this.successMessage.set(null);
    this.errorMessage.set(null);
    this.errorDetails.set([]);

    this.companyService
      .create({ name: name.trim(), website: website.trim() })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (company) => {
          this.successMessage.set(`Company "${company.name}" was created.`);
          this.form.reset();
          this.created.emit(company);
        },
        error: (err: unknown) => {
          const apiError = err instanceof ApiError ? err : null;
          this.errorMessage.set(apiError?.message ?? SERVER_ERROR_MESSAGE);
          // Show extra details (e.g. the relevance explanation) without repeating the headline.
          this.errorDetails.set((apiError?.errors ?? []).filter((e) => e !== apiError?.message));
        },
      });
  }
}
