import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="layout">
      <!-- Mobile only: brand + menu toggle. On wide screens the sidebar is always visible. -->
      <header class="topbar">
        <span class="brand">Company Directory</span>
        <button
          type="button"
          class="menu-toggle"
          (click)="toggleMenu()"
          [attr.aria-expanded]="menuOpen()"
          aria-controls="sidebar"
          aria-label="Toggle navigation"
        >
          <span aria-hidden="true">{{ menuOpen() ? '✕' : '☰' }}</span>
        </button>
      </header>

      <aside id="sidebar" class="sidebar" [class.open]="menuOpen()">
        <div class="brand sidebar-brand">Company Directory</div>
        <nav aria-label="Main">
          @if (auth.isAuthenticated()) {
            <a routerLink="/add" routerLinkActive="active" ariaCurrentWhenActive="page">Add company</a>
            <a routerLink="/search" routerLinkActive="active" ariaCurrentWhenActive="page">Search companies</a>
          }
          <a routerLink="/docs" routerLinkActive="active" ariaCurrentWhenActive="page">Documentation</a>
          @if (!auth.isAuthenticated()) {
            <a routerLink="/login" routerLinkActive="active" ariaCurrentWhenActive="page">Sign in</a>
          }
        </nav>

        @if (auth.isAuthenticated()) {
          <div class="user" data-testid="user">
            <span>Signed in as <strong>{{ auth.username() }}</strong></span>
            <button type="button" class="button ghost" (click)="logout()">Log out</button>
          </div>
        }
      </aside>

      <main class="content">
        <router-outlet />
      </main>
    </div>
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly menuOpen = signal(false);

  constructor() {
    // Close the mobile menu after every navigation.
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => this.menuOpen.set(false));
  }

  toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
