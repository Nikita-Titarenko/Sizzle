import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { RecipeDetails, errorMessage } from '../core/models';
import { Comments } from '../shared/comments';

@Component({
  selector: 'app-recipe-detail',
  imports: [DatePipe, RouterLink, Comments],
  template: `
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (recipe(); as r) {
      <article class="detail">
        @if (r.fileUrl) {
          <img class="cover" [src]="r.fileUrl" [alt]="r.name" />
        }
        <div class="row title">
          <h1>{{ r.name }}</h1>
          <span class="spacer"></span>
          @if (auth.isLoggedIn()) {
            <button class="btn small" [class.outline]="!r.isLiked" (click)="toggleLike()">
              {{ r.isLiked ? '❤️ Liked' : '🤍 Like' }}
            </button>
          }
          @if (isOwner()) {
            <a class="btn small outline" [routerLink]="['/recipes', r.id, 'edit']">Edit</a>
            <button class="btn small danger" (click)="remove()">Delete</button>
          }
        </div>
        <div class="row muted">
          @if (r.userImageUrl) { <img class="avatar" [src]="r.userImageUrl" alt="" /> }
          @else { <span class="avatar">{{ r.userName.charAt(0) }}</span> }
          <a [routerLink]="['/users', r.userId]">{{ r.userName }}</a>
          @if (auth.isLoggedIn() && !isOwner()) {
            <button class="btn small" [class.outline]="r.isFollowed" (click)="toggleFollow()">
              {{ r.isFollowed ? 'Following' : 'Follow' }}
            </button>
          }
          <span>· ⏱ {{ r.cookingTimeMinutes }} min</span>
          <span>· 👁 {{ r.viewCount }}</span>
          <span>· {{ r.creationDate | date: 'mediumDate' }}</span>
        </div>
        <p class="description">{{ r.description }}</p>

        <div class="columns">
          <section class="card">
            <h2>Ingredients</h2>
            <ul class="ingredients">
              @for (i of r.ingredients; track i.id) {
                <li><span>{{ i.name }}</span><span class="muted">{{ i.quantity }} {{ unitLabel(i.unit) }}</span></li>
              }
            </ul>
          </section>

          <section class="card">
            <h2>Steps</h2>
            <ol class="steps">
              @for (step of r.steps; track step.id) {
                <li>
                  <h3>{{ step.name }}</h3>
                  <p>{{ step.description }}</p>
                  <div class="media">
                    @for (url of step.fileUrls; track url) {
                      @if (isVideo(url)) {
                        <video [src]="url" controls></video>
                      } @else {
                        <img [src]="url" alt="" />
                      }
                    }
                  </div>
                </li>
              }
            </ol>
          </section>
        </div>

        <app-comments [recipeId]="r.id" />
      </article>
    } @else if (!error()) {
      <p class="muted">Loading…</p>
    }
  `,
  styles: `
    .detail { display: flex; flex-direction: column; gap: 16px; }
    .cover { width: 100%; max-height: 420px; object-fit: cover; border-radius: var(--radius); }
    .title h1 { margin: 0; }
    .description { white-space: pre-wrap; font-size: 1.05rem; }
    .columns { display: grid; grid-template-columns: 1fr 2fr; gap: 20px; align-items: start; }
    @media (max-width: 760px) { .columns { grid-template-columns: 1fr; } }
    .ingredients { list-style: none; padding: 0; margin: 0; }
    .ingredients li { display: flex; justify-content: space-between; padding: 6px 0; border-bottom: 1px dashed var(--border); }
    .steps { padding-left: 20px; }
    .steps h3 { margin: 0 0 4px; }
    .steps p { white-space: pre-wrap; margin: 0 0 8px; }
    .media { display: flex; gap: 8px; flex-wrap: wrap; }
    .media img, .media video { max-width: 240px; border-radius: 8px; }
  `,
})
export class RecipeDetail {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();
  readonly recipe = signal<RecipeDetails | null>(null);
  readonly error = signal('');

  readonly isOwner = computed(() => !!this.recipe() && this.recipe()!.userId === this.auth.userId());

  constructor() {
    effect(() => {
      const id = this.id();
      this.recipe.set(null);
      this.error.set('');
      this.api.getRecipe(id).subscribe({
        next: (recipe) => this.recipe.set(recipe),
        error: (err) => this.error.set(errorMessage(err)),
      });
      if (this.auth.isLoggedIn()) {
        this.api.updateView(id).subscribe({ error: () => undefined });
      }
    });
  }

  toggleLike(): void {
    const r = this.recipe();
    if (!r) {
      return;
    }
    this.api.toggleLike(r.id).subscribe({
      next: ({ isLiked }) => this.recipe.set({ ...r, isLiked }),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  toggleFollow(): void {
    const r = this.recipe();
    if (!r) {
      return;
    }
    const request = r.isFollowed ? this.api.unfollow(r.userId) : this.api.follow(r.userId);
    request.subscribe({
      next: () => this.recipe.set({ ...r, isFollowed: !r.isFollowed }),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  remove(): void {
    const r = this.recipe();
    if (!r || !confirm(`Delete “${r.name}”?`)) {
      return;
    }
    this.api.deleteRecipe(r.id).subscribe({
      next: () => this.router.navigate(['/profile']),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  unitLabel(unit: string): string {
    return ({ Grams: 'g', Milliliters: 'ml', Pieces: 'pcs' } as Record<string, string>)[unit] ?? unit;
  }

  isVideo(url: string): boolean {
    return /\.(mp4|webm|mov|avi|mkv)$/i.test(url);
  }
}
