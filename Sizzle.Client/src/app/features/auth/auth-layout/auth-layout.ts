import { Component } from '@angular/core';
import { Icon } from '../../../shared/components/icon/icon';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

/** Two-column frame for the sign-in pages: the form on one side, a branded panel on the other. */
@Component({
  selector: 'app-auth-layout',
  imports: [TranslatePipe, Icon],
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.scss',
})
export class AuthLayout {}
