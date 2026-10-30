import { Component, input, output } from '@angular/core';
import { Recipe } from '../../../core/models';
import { CommentSection } from '../../../shared/components/comment-section/comment-section';
import { Icon } from '../../../shared/components/icon/icon';

/** Comments for a feed post: a side panel on desktop, a bottom sheet on phones. */
@Component({
  selector: 'app-comments-drawer',
  imports: [CommentSection, Icon],
  templateUrl: './comments-drawer.html',
  styleUrl: './comments-drawer.scss',
  host: { '(document:keydown.escape)': 'closed.emit()' },
})
export class CommentsDrawer {
  readonly recipe = input.required<Recipe>();
  readonly closed = output<void>();
}
