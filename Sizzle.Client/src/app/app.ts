import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Header } from './layout/header/header';
import { NotificationToasts } from './layout/notification-toasts/notification-toasts';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Header, NotificationToasts],
  templateUrl: './app.html',
})
export class App {}
