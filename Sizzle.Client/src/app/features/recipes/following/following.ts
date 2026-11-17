import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RecipesApi } from '../../../core/api/recipes.api';
import { UsersApi } from '../../../core/api/users.api';
import { I18n } from '../../../core/i18n/i18n.service';
import { FeedRecipe, User } from '../../../core/models';
import { isVideoUrl } from '../../../core/utils/media';
import { Icon } from '../../../shared/components/icon/icon';
import { Loader } from '../../../shared/components/loader/loader';
import { StatusView } from '../../../shared/components/status-view/status-view';
import { UserAvatar } from '../../../shared/components/user-avatar/user-avatar';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

const PAGE_SIZE = 15;

interface Day {
  key: string;
  date: Date;
  /** 0 for today, 1 for yesterday, more for older days. */
  daysAgo: number;
  recipes: FeedRecipe[];
}

/**
 * What the cooks you follow posted, newest first, as a timeline grouped by day: a quiet
 * catch-up list, unlike the full-screen "For you" feed and the Explore grid.
 */
@Component({
  selector: 'app-following',
  imports: [TranslatePipe, RouterLink, DatePipe, Icon, Loader, StatusView, UserAvatar],
  templateUrl: './following.html',
  styleUrl: './following.scss',
})
export class Following {
  private readonly recipesApi = inject(RecipesApi);
  private readonly usersApi = inject(UsersApi);
  private readonly i18n = inject(I18n);

  readonly recipes = signal<FeedRecipe[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly loaded = signal(false);
  readonly error = signal('');
  readonly cooks = signal<User[]>([]);

  readonly hasMore = computed(() => this.recipes().length < this.total());

  readonly days = computed<Day[]>(() => {
    const today = startOfDay(new Date());
    const days: Day[] = [];
    for (const recipe of this.recipes()) {
      const date = new Date(recipe.creationDate);
      const key = startOfDay(date).toDateString();
      let day = days.at(-1);
      if (day?.key !== key) {
        const daysAgo = Math.round((today.getTime() - startOfDay(date).getTime()) / 86_400_000);
        day = { key, date, daysAgo, recipes: [] };
        days.push(day);
      }
      day.recipes.push(recipe);
    }
    return days;
  });

  private pageIndex = 0;

  constructor() {
    this.usersApi.getFollowing().subscribe({
      next: (users) => this.cooks.set(users),
      error: () => undefined,
    });
    this.loadMore();
  }

  loadMore(): void {
    if (this.loading()) {
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.recipesApi.getFollowing(this.pageIndex, PAGE_SIZE).subscribe({
      next: (page) => {
        // The list can shift while paging; skip anything already shown.
        const seen = new Set(this.recipes().map((r) => r.id));
        this.recipes.update((list) => [...list, ...page.data.filter((r) => !seen.has(r.id))]);
        this.total.set(page.totalSize);
        this.pageIndex++;
        this.loading.set(false);
        this.loaded.set(true);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.i18n.error(err));
      },
    });
  }

  protected readonly isVideo = isVideoUrl;
  protected readonly round = Math.round;
}

function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}
