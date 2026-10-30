import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { Comment } from '../../../core/models';
import { errorMessage } from '../../../core/utils/error-message';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-comment-section',
  imports: [FormsModule, DatePipe, RouterLink, NgTemplateOutlet],
  templateUrl: './comment-section.html',
  styleUrl: './comment-section.scss',
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
        this.comments.update((list) =>
          list.filter((c) => c.id !== comment.id && c.parentId !== comment.id),
        );
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
