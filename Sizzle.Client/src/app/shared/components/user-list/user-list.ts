import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { User } from '../../../core/models';

@Component({
  selector: 'app-user-list',
  imports: [RouterLink],
  templateUrl: './user-list.html',
  styleUrl: './user-list.scss',
})
export class UserList {
  readonly users = input.required<User[]>();
  readonly emptyText = input('Nobody here yet.');
}
