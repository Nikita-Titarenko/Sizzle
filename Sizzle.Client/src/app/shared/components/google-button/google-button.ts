import { Component, input } from '@angular/core';
import { TranslatePipe } from '../../pipes/translate.pipe';

/** "Continue with Google" link styled after Google's sign-in button guidelines. */
@Component({
  selector: 'app-google-button',
  imports: [TranslatePipe],
  templateUrl: './google-button.html',
  styleUrl: './google-button.scss',
})
export class GoogleButton {
  readonly href = input.required<string>();
}
