import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'add' },
  {
    path: 'login',
    title: 'Sign in · Company Directory',
    canActivate: [guestGuard],
    loadComponent: () => import('./auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'add',
    title: 'Add company · Company Directory',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/add-company.page').then((m) => m.AddCompanyPage),
  },
  {
    path: 'search',
    title: 'Search companies · Company Directory',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/search-companies.page').then((m) => m.SearchCompaniesPage),
  },
  {
    // Public: describes the API, including how to sign in.
    path: 'docs',
    title: 'Documentation · Company Directory',
    loadComponent: () => import('./pages/documentation.page').then((m) => m.DocumentationPage),
  },
  { path: '**', redirectTo: 'add' },
];
