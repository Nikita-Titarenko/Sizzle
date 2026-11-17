import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { Recipe } from '../../../../core/models';
import { isVideoUrl } from '../../../../core/utils/media';
import { Icon } from '../../../../shared/components/icon/icon';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

/** First screen of "New recipe" when the user has drafts: continue one or start fresh. */
@Component({
  selector: 'app-draft-chooser',
  imports: [DatePipe, Icon, TranslatePipe],
  templateUrl: './draft-chooser.html',
  styleUrl: './draft-chooser.scss',
})
export class DraftChooser {
  readonly drafts = input.required<Recipe[]>();
  readonly error = input('');

  readonly fresh = output<void>();
  readonly open = output<Recipe>();
  readonly remove = output<Recipe>();

  protected readonly isVideo = isVideoUrl;
}
