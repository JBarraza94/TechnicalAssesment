import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, catchError, debounceTime, distinctUntilChanged, map, merge, of, switchMap, tap } from 'rxjs';
import { ApiError, Company } from './company.models';
import { CompanyService, SERVER_ERROR_MESSAGE } from './company.service';

export const SEARCH_DEBOUNCE_MS = 300;

@Component({
  selector: 'app-company-list',
  imports: [ReactiveFormsModule, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <div class="toolbar">
        <h2>Companies</h2>
        <input
          type="search"
          [formControl]="search"
          placeholder="Search by name or domain…"
          aria-label="Search companies"
        />
        <button type="button" (click)="refresh()" [disabled]="loading()">Refresh</button>
      </div>

      @if (error(); as msg) {
        <p class="alert error" role="alert" data-testid="list-error">{{ msg }}</p>
      }

      @if (loading() && companies().length === 0) {
        <p>Loading…</p>
      } @else if (companies().length === 0) {
        <p class="empty" data-testid="empty">No companies found</p>
      } @else {
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Website</th>
                <th>Id</th>
                @if (showScore()) { <th>Score</th> }
              </tr>
            </thead>
            <tbody>
              @for (company of companies(); track company.id) {
                <tr>
                  <td>{{ company.name }}</td>
                  <td><a [href]="company.website" target="_blank" rel="noopener noreferrer">{{ company.website }}</a></td>
                  <td><code>{{ company.id }}</code></td>
                  @if (showScore()) { <td [title]="company.explanation ?? ''">{{ company.score | number: '1.2-2' }}</td> }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
})
export class CompanyListComponent implements OnInit {
  private readonly companyService = inject(CompanyService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload$ = new Subject<void>();

  readonly search = new FormControl('', { nonNullable: true });
  readonly companies = signal<Company[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly showScore = signal(false);

  ngOnInit(): void {
    const typed$ = this.search.valueChanges.pipe(
      debounceTime(SEARCH_DEBOUNCE_MS),
      map((q) => q.trim()),
      distinctUntilChanged(),
    );
    const reloads$ = this.reload$.pipe(map(() => this.search.value.trim()));

    merge(typed$, reloads$)
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap((q) =>
          this.companyService.list(q).pipe(
            map((items) => ({ q, items })),
            catchError((err: unknown) => {
              this.error.set(err instanceof ApiError ? err.message : SERVER_ERROR_MESSAGE);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (result) {
          this.companies.set(result.items);
          this.showScore.set(result.q.length > 0);
        }
      });

    this.refresh();
  }

  refresh(): void {
    this.reload$.next();
  }
}
