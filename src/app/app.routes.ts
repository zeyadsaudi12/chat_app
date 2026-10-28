import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { ChatPageComponent } from './chat/chat-page.component';
import { LoginComponent } from './chat/login.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', component: ChatPageComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];
