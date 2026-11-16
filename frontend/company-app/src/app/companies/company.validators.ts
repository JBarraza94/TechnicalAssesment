import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Mirrors CompanyInputValidator on the server. */
export const NAME_MIN_LENGTH = 2;
export const NAME_MAX_LENGTH = 200;

/** Fails when the trimmed value is empty, or shorter/longer than the limits. */
export const companyNameValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = ((control.value as string | null) ?? '').trim();
  if (!value) return { required: true };
  if (value.length < NAME_MIN_LENGTH) return { minlength: { requiredLength: NAME_MIN_LENGTH, actualLength: value.length } };
  if (value.length > NAME_MAX_LENGTH) return { maxlength: { requiredLength: NAME_MAX_LENGTH, actualLength: value.length } };
  return null;
};

/** Absolute http(s) URL whose host contains a dot (https://www.example.com). */
export const httpUrlValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = ((control.value as string | null) ?? '').trim();
  if (!value) return { required: true };
  if (/\s/.test(value)) return { url: true };

  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return { url: true };
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') return { urlScheme: true };
  const host = url.hostname;
  if (!host.includes('.') || host.startsWith('.') || host.endsWith('.')) return { urlHost: true };
  return null;
};
