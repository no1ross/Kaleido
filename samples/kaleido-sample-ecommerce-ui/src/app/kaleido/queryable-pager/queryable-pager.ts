import {
  Component,
  inject,
  ChangeDetectorRef,
  OnInit
} from '@angular/core';
import { Subscription } from 'rxjs';
import {
  QueryExecutionStateService, QueryResultStateService
} from '../services/query-state-service';


@Component({
  selector: 'kaleido-queryable-pager',
  imports: [],
  templateUrl: './queryable-pager.html',
  styleUrl: './queryable-pager.scss',
})

export class QueryablePager implements OnInit {

  totalCount = 0;

  private readonly queryState =
    inject(QueryExecutionStateService);
    
  private readonly resultState =
    inject(QueryResultStateService);

  private resultSubscription?: Subscription;

  private readonly changeDetector =
    inject(ChangeDetectorRef);

    ngOnInit(): void {
      this.resultSubscription =
        this.resultState.changed
            .subscribe(() => {
                this.totalCount = this.resultState.state.totalCount;

                this.changeDetector.detectChanges();
            });
  }

  ngOnDestroy(): void {
      this.resultSubscription?.unsubscribe();
  }

  private get page() {
      return this.queryState.state.request.query?.page;
  }

  get offset(): number {

    return this.page?.offset ?? 0;
  }

  get pageSize(): number {

    return this.page?.size ?? 25;
  }

  get currentPage(): number {

      if (
          this.totalCount === 0 ||
          this.pageSize <= 0) {
          return 0;
      }

      return Math.floor(
          this.offset / this.pageSize)
          + 1;
  }

  get totalPages(): number {

      if (
          this.totalCount === 0 ||
          this.pageSize <= 0) {
          return 0;
      }

      return Math.ceil(
          this.totalCount / this.pageSize);
  }

  get startRecord(): number {

    if (this.totalCount === 0) {
      return 0;
    }

    return this.offset + 1;
  }

  get endRecord(): number {

    return Math.min(
      this.offset + this.pageSize,
      this.totalCount);
  }

  get canGoBackward(): boolean {

    return this.offset > 0;
  }

  get canGoForward(): boolean {

    return this.currentPage < this.totalPages;
  }

  firstPage(): void {

    this.emitPageChanged(
      0,
      this.pageSize);
  }

  previousPage(): void {

    if (!this.canGoBackward) {
      return;
    }

    this.emitPageChanged(
      Math.max(
        0,
        this.offset - this.pageSize),
      this.pageSize);
  }

  nextPage(): void {

      console.log('NEXT PAGE');

      console.log(
          'currentPage',
          this.currentPage);

      console.log(
          'totalPages',
          this.totalPages);

      console.log(
          'canGoForward',
          this.canGoForward);

      if (!this.canGoForward) {
          return;
      }

      this.emitPageChanged(
          this.offset + this.pageSize,
          this.pageSize);
  }

  lastPage(): void {

    if (this.totalPages === 0) {
      return;
    }

    this.emitPageChanged(
      (this.totalPages - 1)
        * this.pageSize,
      this.pageSize);
  }

  pageSizeChanged(
      event: Event): void {

      const select =
          event.target as HTMLSelectElement;

      const pageSize =
          Number(
              select.value);

      if (
          Number.isNaN(
              pageSize) ||
          pageSize <= 0) {
          return;
      }

      this.emitPageChanged(
          0,
          pageSize);
  }

  private emitPageChanged(
      offset: number,
      pageSize: number): void {

      console.log(
          'SETTING PAGE',
          offset,
          pageSize);

      this.queryState.state.request.query ??= {};

      this.queryState.state.request.query.page = {
          offset,
          size: pageSize
      };

      console.log(
          'NEW PAGE STATE',
          this.queryState.state.request.query.page);

      this.queryState.notifyChanged();
  }
}