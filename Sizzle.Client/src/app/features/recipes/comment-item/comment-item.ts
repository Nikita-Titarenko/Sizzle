import { DatePipe } from '@angular/common';
import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Comment } from '../../../core/models';

/** A single comment with inline reply and edit forms. API calls are left to the parent. */
@Component({
  selector: 'app-comment-item',
  imports: [FormsModule, DatePipe, RouterLink],
  templateUrl: './comment-item.html',
  styleUrl: './comment-item.scss',
})
export class CommentItem {
  readonly comment = input.required<Comment>();
  readonly canReply = input(false);
  readonly canModify = input(false);

  readonly replied = output<string>();
  readonly edited = output<string>();
  readonly deleted = output<void>();

  readonly mode = signal<'view' | 'edit' | 'reply'>('view');
  editText = '';
  replyText = '';

  toggleReply(): void {
    this.replyText = '';
    this.mode.set(this.mode() === 'reply' ? 'view' : 'reply');
  }

  startEdit(): void {
    this.editText = this.comment().content;
    this.mode.set('edit');
  }

  submitEdit(): void {
    this.edited.emit(this.editText.trim());
    this.mode.set('view');
  }

  submitReply(): void {
    this.replied.emit(this.replyText.trim());
    this.mode.set('view');
  }
}
