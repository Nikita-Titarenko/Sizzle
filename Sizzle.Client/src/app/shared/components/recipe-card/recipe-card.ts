import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { isVideoUrl } from '../../../core/utils/media';
import { Recipe } from '../../../core/models';
import { UserAvatar } from '../user-avatar/user-avatar';

@Component({
  selector: 'app-recipe-card',
  imports: [UserAvatar, RouterLink, DatePipe],
  templateUrl: './recipe-card.html',
  styleUrl: './recipe-card.scss',
})
export class RecipeCard {
  readonly recipe = input.required<Recipe>();
  readonly isVideo = computed(() => isVideoUrl(this.recipe().fileUrl));
}
