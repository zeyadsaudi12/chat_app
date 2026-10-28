import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, ButtonModule, InputTextModule],
  template: `
    <form class="card" (ngSubmit)="submit()">
      <h2>{{ registering() ? 'Create account' : 'Sign in' }}</h2>

      <input pInputText name="u" [(ngModel)]="userName" placeholder="Username" autocomplete="username" required />
      @if (registering()) {
        <input pInputText name="d" [(ngModel)]="displayName" placeholder="Display name" required />
      }
      <input pInputText name="p" type="password" [(ngModel)]="password" placeholder="Password (min 8 chars)"
             autocomplete="current-password" required />

      @if (error()) { <small class="error">{{ error() }}</small> }

      <p-button type="submit" [label]="registering() ? 'Register' : 'Sign in'" [loading]="busy()" />
      <button type="button" class="link" (click)="registering.set(!registering())">
        {{ registering() ? 'I already have an account' : 'Create an account' }}
      </button>
    </form>
  `,
  styles: [`
    :host { display: grid; place-items: center; min-height: 100vh; }
    .card { display: flex; flex-direction: column; gap: 12px; width: 320px; padding: 24px;
            border: 1px solid var(--p-content-border-color); border-radius: 12px; }
    .error { color: var(--p-red-500); }
    .link { background: none; border: 0; cursor: pointer; color: var(--p-primary-color); }
  `]
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  userName = ''; displayName = ''; password = '';
  readonly registering = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');

  async submit() {
    this.busy.set(true);
    this.error.set('');
    try {
      if (this.registering()) await this.auth.register(this.userName, this.displayName, this.password);
      else await this.auth.login(this.userName, this.password);
      await this.router.navigateByUrl('/');
    } catch (e: any) {
      this.error.set(typeof e?.error === 'string' ? e.error : 'Something went wrong. Try again.');
    } finally {
      this.busy.set(false);
    }
  }
}
