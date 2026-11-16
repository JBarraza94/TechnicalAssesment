import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CompanyFormComponent } from '../companies/company-form.component';

@Component({
  selector: 'app-add-company-page',
  imports: [CompanyFormComponent, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <h1>Add company</h1>
      <a class="button secondary" routerLink="/search" data-testid="go-search">Search company</a>
    </header>
    <app-company-form />
  `,
})
export class AddCompanyPage {}
