import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/recipe-list').then((m) => m.RecipeList) },
  { path: 'login', loadComponent: () => import('./pages/login').then((m) => m.Login) },
  { path: 'register', loadComponent: () => import('./pages/register').then((m) => m.Register) },
  {
    path: 'confirm-email/:userId',
    loadComponent: () => import('./pages/confirm-email').then((m) => m.ConfirmEmail),
  },
  {
    path: 'auth/google-callback',
    loadComponent: () => import('./pages/google-callback').then((m) => m.GoogleCallback),
  },
  {
    path: 'recipes/new',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/recipe-form').then((m) => m.RecipeForm),
  },
  {
    path: 'recipes/:id/edit',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/recipe-form').then((m) => m.RecipeForm),
  },
  { path: 'recipes/:id', loadComponent: () => import('./pages/recipe-detail').then((m) => m.RecipeDetail) },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/profile').then((m) => m.Profile),
  },
  { path: 'users/:id', loadComponent: () => import('./pages/user-page').then((m) => m.UserPage) },
  { path: '**', redirectTo: '' },
];
