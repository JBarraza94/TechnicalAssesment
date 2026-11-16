import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CompanyListComponent } from '../companies/company-list.component';

@Component({
  selector: 'app-search-companies-page',
  imports: [CompanyListComponent, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <h1>Search companies</h1>
      <a class="button secondary" routerLink="/add" data-testid="go-add">Add company</a>
    </header>
    <app-company-list />
  `,
})
export class SearchCompaniesPage {}
