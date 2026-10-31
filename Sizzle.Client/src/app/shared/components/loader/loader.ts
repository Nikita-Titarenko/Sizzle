import { Component, input } from '@angular/core';
import { TranslatePipe } from '../../pipes/translate.pipe';

/** Spinner with the app emblem, used while a page or list is loading. */
@Component({
  selector: 'app-loader',
  imports: [TranslatePipe],
  templateUrl: './loader.html',
  styleUrl: './loader.scss',
  host: { role: 'status' },
})
export class Loader {
  /** Text under the spinner; defaults to "Loading…". */
  readonly label = input('');
}
