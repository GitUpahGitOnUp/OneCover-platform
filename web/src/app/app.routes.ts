import { Routes } from '@angular/router';
import { adminGuard, authGuard, dashboardGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/auth-page').then((module) => module.AuthPage),
    data: { mode: 'login' },
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/auth-page').then((module) => module.AuthPage),
    data: { mode: 'register' },
  },
  {
    path: 'dashboard',
    canActivate: [authGuard, dashboardGuard],
    loadComponent: () =>
      import('./features/dashboard/dashboard-page').then((module) => module.DashboardPage),
  },
  {
    path: 'admin',
    canActivate: [authGuard, adminGuard],
    loadComponent: () =>
      import('./features/admin/admin-dashboard-page').then((module) => module.AdminDashboardPage),
  },
  {
    path: 'coverage',
    loadComponent: () =>
      import('./features/coverage/coverage-page').then((module) => module.CoveragePage),
  },
  {
    path: 'claims',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/claims/claims-page').then((module) => module.ClaimsPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
