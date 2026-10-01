import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';

// Every feature is lazy-loaded so the initial bundle only contains the shell.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tasks' },
  {
    path: 'login',
    title: 'Log in · TaskApp',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    title: 'Create account · TaskApp',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'tasks',
    canActivate: [authGuard],
    loadChildren: () => import('./features/tasks/tasks.routes').then((m) => m.TASK_ROUTES),
  },
  { path: '**', redirectTo: 'tasks' },
];
