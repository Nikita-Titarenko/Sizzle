import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { User } from '../../../core/models';

@Component({
  selector: 'app-user-list',
  imports: [RouterLink],
  template: `
    <ul class="users">
      @for (u of users(); track u.id) {
        <li>
          <a class="row" [routerLink]="['/users', u.id]">
            @if (u.imageUrl) { <img class="avatar" [src]="u.imageUrl" alt="" /> }
            @else { <span class="avatar">{{ u.name.charAt(0) }}</span> }
            {{ u.name }}
          </a>
        </li>
      } @empty {
        <li class="muted">{{ emptyText() }}</li>
      }
    </ul>
  `,
  styles: `.users { list-style: none; padding: 0; margin: 0; } .users li { padding: 6px 0; } .row { gap: 8px; }`,
})
export class UserList {
  readonly users = input.required<User[]>();
  readonly emptyText = input('Nobody here yet.');
}
