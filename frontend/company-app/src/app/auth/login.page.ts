import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiError } from '../companies/company.models';
import { SERVER_ERROR_MESSAGE } from '../companies/company.service';
import { safeReturnUrl } from './auth.guards';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="login-card">
        <h1>Sign in</h1>
        <p class="muted">Sign in to add and search companies.</p>

        @if (sessionExpired) {
          <p class="alert info" role="status" data-testid="expired">Your session has expired. Please sign in again.</p>
        }

        <label for="username">Username</label>
        <input id="username" formControlName="username" autocomplete="username" autocapitalize="none" />
        @if (form.controls.username.touched && form.controls.username.invalid) {
          <small class="field-error">Username is required.</small>
        }

        <label for="password">Password</label>
        <input id="password" type="password" formControlName="password" autocomplete="current-password" />
        @if (form.controls.password.touched && form.controls.password.invalid) {
          <small class="field-error">Password is required.</small>
        }

        <button type="submit" [disabled]="form.invalid || submitting()">
          {{ submitting() ? 'Signing in…' : 'Sign in' }}
        </button>

        @if (errorMessage(); as msg) {
          <p class="alert error" role="alert" data-testid="login-error">{{ msg }}</p>
        }
      </form>
    </div>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly sessionExpired = this.route.snapshot.queryParamMap.get('reason') === 'expired';

  readonly form = new FormGroup({
    username: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;

    const { username, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.errorMessage.set(null);

    this.auth
      .login(username.trim(), password)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          const target = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
          void this.router.navigateByUrl(target);
        },
        error: (err: unknown) => {
          this.errorMessage.set(err instanceof ApiError ? err.message : SERVER_ERROR_MESSAGE);
          this.form.controls.password.reset();
        },
      });
  }
}
