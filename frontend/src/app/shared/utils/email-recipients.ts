const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const EMAIL_SPLIT_PATTERN = /[;,\r\n]+/;

export function splitEmailRecipients(value: string | null | undefined): string[] {
  return (value ?? '')
    .split(EMAIL_SPLIT_PATTERN)
    .map((recipient) => recipient.trim())
    .filter(Boolean);
}

export function isValidEmailRecipient(value: string | null | undefined): value is string {
  const candidate = value?.trim() ?? '';
  return !!candidate && !/[;,\r\n]/.test(candidate) && EMAIL_PATTERN.test(candidate);
}

export function isValidEmailAddress(value: string | null | undefined): value is string {
  return isValidEmailRecipient(value);
}

export function parseEmailRecipients(value: string | null | undefined): string[] {
  return dedupeCaseInsensitive(
    splitEmailRecipients(value).filter((recipient) => isValidEmailRecipient(recipient)),
  );
}

export function findInvalidEmailRecipients(value: string | null | undefined): string[] {
  return dedupeCaseInsensitive(
    splitEmailRecipients(value).filter((recipient) => !isValidEmailRecipient(recipient)),
  );
}

export function joinEmailRecipients(recipients: readonly string[] | null | undefined): string {
  return dedupeCaseInsensitive(
    (recipients ?? []).map((recipient) => recipient.trim()).filter(Boolean),
  ).join('; ');
}

export function formatEmailRecipientsInput(value: string | null | undefined): string {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) {
    return '';
  }

  // Preserve invalid input so the caller can surface it instead of silently dropping it.
  return findInvalidEmailRecipients(trimmed).length > 0
    ? trimmed
    : joinEmailRecipients(parseEmailRecipients(trimmed));
}

function dedupeCaseInsensitive(values: readonly string[]): string[] {
  const seen = new Set<string>();
  const deduped: string[] = [];

  values.forEach((value) => {
    const key = value.toLowerCase();
    if (seen.has(key)) {
      return;
    }

    seen.add(key);
    deduped.push(value);
  });

  return deduped;
}
