import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    providePrimeNG({
      license: 'eyJpZCI6IjMzMTI1MDIyLTIxMjgtNDE4Yy1hMmY1LTQ3ZjVlZmIzOTQ0MSIsInByb2R1Y3QiOiJwcmltZXVpIiwidGllciI6ImNvbW11bml0eSIsInR5cGUiOiJkZXYiLCJpYXQiOjE3OTI3MTc1MDYsImV4cCI6MTgyNDI1MzUwNn0.hcJHYX1sFioI3MprjrjZ0Bt2WbMCeLCu3tghMrgMbwWFs0IegCJ5gWlyg-FgUOwxdd8cfCxKG1ZdQ0NOlFAABQ',
      theme: { preset: Aura }
    })
  ]
};
