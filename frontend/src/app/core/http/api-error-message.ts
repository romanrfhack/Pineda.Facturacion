const KNOWN_ERROR_MESSAGES: Record<string, string> = {
  Forbidden: 'Acceso denegado.',
  Unauthorized: 'No autorizado.',
  'Invalid credentials': 'Credenciales inválidas.',
  'Not found': 'No encontrado.',
  'Provider unavailable': 'PAC no disponible.',
  'Provider is unavailable': 'PAC no disponible.',
  'PAC provider is unavailable. Retry after checking status.': 'PAC no disponible. Intenta de nuevo después de verificar el estatus.',
  'Applied invoices belong to different receivers.': 'Las facturas aplicadas pertenecen a receptores distintos.'
};

export function extractApiErrorMessage(error: unknown, fallback = 'No se pudo completar la operación.'): string {
  if (typeof error === 'object' && error && 'error' in error) {
    const payload = (error as {
      error?: {
        errorMessage?: string;
        detail?: string;
        title?: string;
        errors?: Record<string, string[]>;
      };
    }).error;

    if (payload?.errorMessage) {
      return KNOWN_ERROR_MESSAGES[payload.errorMessage] ?? payload.errorMessage;
    }

    const validationErrors = payload?.errors
      ? Object.values(payload.errors)
        .flat()
        .map((message) => KNOWN_ERROR_MESSAGES[message] ?? message)
      : [];

    if (validationErrors.length) {
      return validationErrors.join(' ');
    }

    if (payload?.detail) {
      return KNOWN_ERROR_MESSAGES[payload.detail] ?? payload.detail;
    }

    if (payload?.title) {
      return KNOWN_ERROR_MESSAGES[payload.title] ?? payload.title;
    }
  }

  return fallback;
}
