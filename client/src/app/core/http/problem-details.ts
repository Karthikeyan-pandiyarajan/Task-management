import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 body returned by the API's global exception handler. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && ('title' in value || 'detail' in value);
}

/** Turns any HTTP failure into one human-readable message for the UI. */
export function toErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }
  if (error.status === 0) {
    return 'Cannot reach the server. Check that the APIs are running.';
  }
  const body: unknown = error.error;
  if (isProblemDetails(body)) {
    const validation = body.errors ? Object.values(body.errors).flat()[0] : undefined;
    return validation ?? body.detail ?? body.title ?? fallback;
  }
  return fallback;
}
