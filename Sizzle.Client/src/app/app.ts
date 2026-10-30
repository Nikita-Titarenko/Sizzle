import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { Header } from './layout/header/header';
import { NotificationToasts } from './layout/notification-toasts/notification-toasts';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Header, NotificationToasts],
  templateUrl: './app.html',
})
export class App {
  private readonly route = inject(ActivatedRoute);

  /** Routes with `data: { fullBleed: true }` (the feed) take the whole area under the header. */
  protected readonly fullBleed = toSignal(
    inject(Router).events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => {
        let route = this.route.snapshot;
        while (route.firstChild) {
          route = route.firstChild;
        }
        return route.data['fullBleed'] === true;
      }),
    ),
    { initialValue: false },
  );
}
