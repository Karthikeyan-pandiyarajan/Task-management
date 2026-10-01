import { Routes } from '@angular/router';

export const TASK_ROUTES: Routes = [
  {
    path: '',
    title: 'My tasks · TaskApp',
    loadComponent: () => import('./task-list/task-list').then((m) => m.TaskList),
  },
  {
    path: 'new',
    title: 'New task · TaskApp',
    loadComponent: () => import('./task-form/task-form').then((m) => m.TaskForm),
  },
  {
    path: ':id/edit',
    title: 'Edit task · TaskApp',
    loadComponent: () => import('./task-form/task-form').then((m) => m.TaskForm),
  },
];
