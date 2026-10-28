import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_URL } from './config';

interface Session { token: string; userId: string; displayName: string; }
const KEY = 'chat.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private session = signal<Session | null>(this.load());

  readonly token = computed(() => this.session()?.token ?? null);
  readonly userId = computed(() => this.session()?.userId ?? '');
  readonly displayName = computed(() => this.session()?.displayName ?? '');
  readonly isLoggedIn = computed(() => !!this.session());

  async login(userName: string, password: string) {
    this.save(await firstValueFrom(
      this.http.post<Session>(`${API_URL}/auth/login`, { userName, password })));
  }

  async register(userName: string, displayName: string, password: string) {
    this.save(await firstValueFrom(
      this.http.post<Session>(`${API_URL}/auth/register`, { userName, displayName, password })));
  }

  logout() {
    localStorage.removeItem(KEY);
    this.session.set(null);
  }

  private save(s: Session) {
    localStorage.setItem(KEY, JSON.stringify(s));
    this.session.set(s);
  }

  private load(): Session | null {
    try { return JSON.parse(localStorage.getItem(KEY) ?? 'null'); } catch { return null; }
  }
}
