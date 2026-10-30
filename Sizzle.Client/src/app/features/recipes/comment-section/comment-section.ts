import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { Comment, errorMessage } from '../../../core/models';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-comment-section',
  imports: [FormsModule, DatePipe, RouterLink, NgTemplateOutlet],
  template: `
    <section class="card">
      <h2>Comments ({{ total() }})</h2>

      @if (auth.isLoggedIn()) {
        <form class="form" (ngSubmit)="post(null, draft)">
          <textarea name="draft" [(ngModel)]="draft" placeholder="Share your thoughts…" maxlength="2000"></textarea>
          <div><button class="btn small" [disabled]="!draft.trim()">Post</button></div>
        </form>
      } @else {
        <p class="muted"><a routerLink="/login">Log in</a> to leave a comment.</p>
      }
      @if (error()) { <p class="error">{{ error() }}</p> }

      @for (comment of roots(); track comment.id) {
        <ng-container *ngTemplateOutlet="item; context: { $implicit: comment }" />
        <div class="replies">
          @for (reply of repliesOf(comment.id); track reply.id) {
            <ng-container *ngTemplateOutlet="item; context: { $implicit: reply }" />
          }
        </div>
      } @empty {
        <p class="muted">No comments yet.</p>
      }

      @if (comments().length < total()) {
        <button class="btn small outline" (click)="loadMore()">Load more</button>
      }
    </section>

    <ng-template #item let-c>
      <div class="comment">
        <div class="row head">
          @if (c.userImageUrl) { <img class="avatar" [src]="c.userImageUrl" alt="" /> }
          @else { <span class="avatar">{{ c.userName.charAt(0) }}</span> }
          <a [routerLink]="['/users', c.userId]"><b>{{ c.userName }}</b></a>
          <span class="muted">{{ c.createdAt | date: 'short' }}</span>
        </div>
        @if (editingId() === c.id) {
          <form class="form" (ngSubmit)="saveEdit(c)">
            <textarea name="edit" [(ngModel)]="editText"></textarea>
            <div class="row">
              <button class="btn small">Save</button>
              <button type="button" class="link" (click)="editingId.set(null)">Cancel</button>
            </div>
          </form>
        } @else {
          <p class="content">{{ c.content }}</p>
        }
        <div class="row actions">
          @if (auth.isLoggedIn() && !c.parentId) {
            <button class="link" (click)="replyTo.set(replyTo() === c.id ? null : c.id)">Reply</button>
          }
          @if (c.userId === auth.userId()) {
            <button class="link" (click)="startEdit(c)">Edit</button>
            <button class="link" (click)="remove(c)">Delete</button>
          }
        </div>
        @if (replyTo() === c.id) {
          <form class="form reply" (ngSubmit)="post(c.id, replyText)">
            <textarea name="reply" [(ngModel)]="replyText" placeholder="Write a reply…"></textarea>
            <div><button class="btn small" [disabled]="!replyText.trim()">Reply</button></div>
          </form>
        }
      </div>
    </ng-template>
  `,
  styles: `
    .comment { padding: 12px 0; border-top: 1px solid var(--border); }
    .head { gap: 8px; font-size: 0.9rem; }
    .content { margin: 8px 0; white-space: pre-wrap; }
    .actions { gap: 14px; font-size: 0.85rem; }
    .replies { margin-left: 40px; }
    .reply { margin-top: 8px; }
  `,
})
export class CommentSection implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly auth = inject(AuthService);

  readonly recipeId = input.required<string>();

  readonly comments = signal<Comment[]>([]);
  readonly total = signal(0);
  readonly error = signal('');
  readonly replyTo = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  private pageIndex = 0;
  draft = '';
  replyText = '';
  editText = '';

  readonly roots = computed(() => this.comments().filter((c) => !c.parentId));

  ngOnInit(): void {
    this.load(0);
  }

  repliesOf(id: string): Comment[] {
    return this.comments().filter((c) => c.parentId === id);
  }

  loadMore(): void {
    this.load(this.pageIndex + 1);
  }

  post(parentId: string | null, content: string): void {
    this.api.createComment(this.recipeId(), content.trim(), parentId).subscribe({
      next: (comment) => {
        this.comments.update((list) => (parentId ? [...list, comment] : [comment, ...list]));
        this.total.update((n) => n + 1);
        this.draft = '';
        this.replyText = '';
        this.replyTo.set(null);
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  startEdit(comment: Comment): void {
    this.editText = comment.content;
    this.editingId.set(comment.id);
  }

  saveEdit(comment: Comment): void {
    this.api.updateComment(comment.id, this.editText.trim()).subscribe({
      next: (updated) => {
        this.comments.update((list) => list.map((c) => (c.id === updated.id ? updated : c)));
        this.editingId.set(null);
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  remove(comment: Comment): void {
    if (!confirm('Delete this comment?')) {
      return;
    }
    this.api.deleteComment(comment.id).subscribe({
      next: () => {
        this.comments.update((list) => list.filter((c) => c.id !== comment.id && c.parentId !== comment.id));
        this.total.update((n) => n - 1);
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  private load(pageIndex: number): void {
    this.api.getComments(this.recipeId(), pageIndex, PAGE_SIZE).subscribe({
      next: (page) => {
        this.pageIndex = pageIndex;
        this.total.set(page.totalSize);
        this.comments.update((list) => {
          const known = new Set(list.map((c) => c.id));
          return [...list, ...page.data.filter((c) => !known.has(c.id))];
        });
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}
