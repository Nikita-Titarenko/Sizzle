import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NotificationsService } from '../../core/services/notifications.service';
import { Icon } from '../../shared/components/icon/icon';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-notification-toasts',
  imports: [TranslatePipe, Icon, RouterLink],
  templateUrl: './notification-toasts.html',
  styleUrl: './notification-toasts.scss',
})
export class NotificationToasts {
  protected readonly notifications = inject(NotificationsService);
}
