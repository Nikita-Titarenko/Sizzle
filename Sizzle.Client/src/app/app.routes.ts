import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    data: { fullBleed: true },
    loadComponent: () => import('./features/feed/feed-page/feed-page').then((m) => m.FeedPage),
  },
  {
    path: 'explore',
    loadComponent: () => import('./features/recipes/explore/explore').then((m) => m.Explore),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'confirm-email/:userId',
    loadComponent: () =>
      import('./features/auth/confirm-email/confirm-email').then((m) => m.ConfirmEmail),
  },
  {
    path: 'auth/google-callback',
    loadComponent: () =>
      import('./features/auth/google-callback/google-callback').then((m) => m.GoogleCallback),
  },
  {
    path: 'recipes/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/recipes/recipe-form/recipe-form').then((m) => m.RecipeForm),
  },
  {
    path: 'recipes/:id/edit',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/recipes/recipe-form/recipe-form').then((m) => m.RecipeForm),
  },
  {
    path: 'recipes/:id',
    loadComponent: () =>
      import('./features/recipes/recipe-detail/recipe-detail').then((m) => m.RecipeDetail),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/users/profile/profile').then((m) => m.Profile),
  },
  {
    path: 'users/:id',
    loadComponent: () => import('./features/users/user-page/user-page').then((m) => m.UserPage),
  },
  { path: '**', redirectTo: '' },
];
