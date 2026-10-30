import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pager',
  template: `
    @if (pages() > 1) {
      <div class="row pager">
        <button class="btn small outline" [disabled]="pageIndex() === 0" (click)="change.emit(pageIndex() - 1)">←</button>
        <span class="muted">Page {{ pageIndex() + 1 }} of {{ pages() }}</span>
        <button class="btn small outline" [disabled]="pageIndex() + 1 >= pages()" (click)="change.emit(pageIndex() + 1)">→</button>
      </div>
    }
  `,
  styles: `.pager { justify-content: center; margin-top: 24px; }`,
})
export class Pager {
  readonly pageIndex = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly change = output<number>();
  readonly pages = computed(() => Math.ceil(this.total() / this.pageSize()));
}
