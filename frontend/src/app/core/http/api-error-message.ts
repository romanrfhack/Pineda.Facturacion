export function extractApiErrorMessage(error: unknown, fallback = 'The operation could not be completed.'): string {
  if (typeof error === 'object' && error && 'error' in error) {
    const payload = (error as { error?: { errorMessage?: string } }).error;
    if (payload?.errorMessage) {
      return payload.errorMessage;
    }
  }

  return fallback;
}
