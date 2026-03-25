export interface FiscalDocumentFileNameParts {
  issuerRfc: string;
  receiverRfc: string;
  series?: string | null;
  folio?: string | number | null;
  fallbackToken?: string | number | null;
}

export function buildFiscalDocumentFileName(
  parts: FiscalDocumentFileNameParts,
  extension: string
): string {
  const documentToken = `${parts.series ?? ''}${parts.folio ?? ''}`.trim();
  const middleToken = documentToken.length > 0
    ? documentToken
    : String(parts.fallbackToken ?? 'CFDI').trim();

  return `${sanitizeFileToken(parts.issuerRfc)}_${sanitizeFileToken(middleToken)}_${sanitizeFileToken(parts.receiverRfc)}.${extension.replace(/^\.+/, '')}`;
}

function sanitizeFileToken(value: string): string {
  const sanitized = value
    .trim()
    .replace(/[\\/:*?"<>|]+/g, '_')
    .replace(/\s+/g, '');

  return sanitized || 'CFDI';
}
