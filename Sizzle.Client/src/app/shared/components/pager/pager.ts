import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pager',
  templateUrl: './pager.html',
  styleUrl: './pager.scss',
})
export class Pager {
  readonly pageIndex = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly change = output<number>();
  readonly pages = computed(() => Math.ceil(this.total() / this.pageSize()));
}
