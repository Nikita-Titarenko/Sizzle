import {
  Component,
  ElementRef,
  computed,
  effect,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Recipe } from '../../../core/models';
import { isVideoUrl } from '../../../core/utils/media';
import { Icon } from '../../../shared/components/icon/icon';
import { UserAvatar } from '../../../shared/components/user-avatar/user-avatar';

/**
 * One full-height post in the feed: media on the left, action rail on the right
 * (overlaid on small screens). Videos play only while the post is the active one.
 */ import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-feed-item',
  imports: [TranslatePipe, RouterLink, Icon, UserAvatar],
  templateUrl: './feed-item.html',
  styleUrl: './feed-item.scss',
})
export class FeedItem {
  readonly recipe = input.required<Recipe>();
  readonly active = input(false);
  readonly categoryName = input<string>();
  readonly canFollow = input(true);
  /** Shared across the feed so the sound setting survives scrolling. */
  readonly muted = input(true);

  readonly liked = output<void>();
  readonly followed = output<void>();
  readonly commentsOpened = output<void>();
  readonly shared = output<void>();
  readonly mutedChange = output<boolean>();

  readonly isVideo = computed(() => isVideoUrl(this.recipe().fileUrl));
  readonly expanded = signal(false);
  readonly paused = signal(false);
  readonly heartBurst = signal(false);

  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');

  constructor() {
    effect(() => {
      const video = this.video()?.nativeElement;
      if (!video) {
        return;
      }
      if (this.active()) {
        video.play().catch(() => this.paused.set(true));
        this.paused.set(false);
      } else {
        video.pause();
      }
    });
  }

  togglePlay(): void {
    const video = this.video()?.nativeElement;
    if (!video) {
      return;
    }
    if (video.paused) {
      void video.play();
      this.paused.set(false);
    } else {
      video.pause();
      this.paused.set(true);
    }
  }

  /** Instagram-style double tap: always likes, never unlikes. */
  likeFromMedia(): void {
    if (!this.recipe().isLiked) {
      this.liked.emit();
    }
    this.heartBurst.set(true);
    setTimeout(() => this.heartBurst.set(false), 700);
  }
}
