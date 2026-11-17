/** ErrorKey values the client reacts to (the API sends the enum as a number). */
export const ErrorKeys = {
  forbidden: 7,
  recipeRejected: 8,
  recipeNeedsCorrection: 9,
} as const;

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
