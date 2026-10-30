import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NotificationsService } from '../../core/services/notifications.service';

@Component({
  selector: 'app-notification-toasts',
  imports: [RouterLink],
  templateUrl: './notification-toasts.html',
  styleUrl: './notification-toasts.scss',
})
export class NotificationToasts {
  protected readonly notifications = inject(NotificationsService);
}
