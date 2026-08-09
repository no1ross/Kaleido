import {
  Component,
  EventEmitter,
  Input,
  Output
} from '@angular/core';

import {
  QueryRequest
} from '../models/queryable-request';

@Component({
  selector: 'kaleido-queryable-pager',
  imports: [],
  templateUrl: './queryable-pager.html',
  styleUrl: './queryable-pager.scss',
})

export class QueryablePager {

  @Input({ required: true })
  queryRequest!: QueryRequest;

  @Input()
  totalCount = 0;

  @Output()
  queryRequestChanged =
    new EventEmitter<QueryRequest>();

  get offset(): number {

    return this.queryRequest
      .query
      .page
      .offset;
  }

  get pageSize(): number {

    return this.queryRequest
      .query
      .page
      .size;
  }

  get currentPage(): number {

    if (this.totalCount === 0) {
      return 0;
    }

    return Math.floor(
      this.offset / this.pageSize)
      + 1;
  }

  get totalPages(): number {

    if (this.totalCount === 0) {
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

    this.emitPageChanged(
      0,
      Number(
        select.value));
  }

  private emitPageChanged(
    offset: number,
    pageSize: number): void {

    const queryRequest =
      structuredClone(
        this.queryRequest);

    queryRequest.query.page.offset =
      offset;

    queryRequest.query.page.size =
      pageSize;

    this.queryRequestChanged.emit(
      queryRequest);
  }
}