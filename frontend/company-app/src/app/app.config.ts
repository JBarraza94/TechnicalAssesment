import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { environment } from '../environments/environment';
import { API_BASE_URL } from './companies/company.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch()),
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
  ],
};
