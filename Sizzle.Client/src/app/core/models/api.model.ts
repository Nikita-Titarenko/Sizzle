export interface ApiError {
  key: number | string;
  field?: string;
  message: string;
}

export interface PageResponse<T> {
  data: T[];
  totalSize: number;
  pageIndex: number;
  pageSize: number;
}
