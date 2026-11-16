import { ChangeDetectionStrategy, Component, viewChild } from '@angular/core';
import { CompanyFormComponent } from './companies/company-form.component';
import { CompanyListComponent } from './companies/company-list.component';

@Component({
  selector: 'app-root',
  imports: [CompanyFormComponent, CompanyListComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main>
      <h1>Company Directory</h1>
      <app-company-form (created)="list().refresh()" />
      <app-company-list />
    </main>
  `,
})
export class App {
  protected readonly list = viewChild.required(CompanyListComponent);
}
