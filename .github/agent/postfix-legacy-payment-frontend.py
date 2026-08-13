from pathlib import Path

path = Path('frontend/src/app/features/fiscal-documents/pages/fiscal-document-operations-page.component.ts')
text = path.read_text(encoding='utf-8-sig')
old = '''      if (!billingDocument.fiscalDocumentId) {
        await this.loadLegacyPaymentSuggestion(
          billingDocument.billingDocumentId,
          !this.paymentFieldsEditedByUser,
        );
      } else {
        this.legacyPaymentSuggestion.set(null);
      }'''
new = '''      if (
        !billingDocument.fiscalDocumentId &&
        !preserveCurrentFiscalDocument &&
        !this.fiscalDocument()
      ) {
        await this.loadLegacyPaymentSuggestion(
          billingDocument.billingDocumentId,
          !this.paymentFieldsEditedByUser,
        );
      } else {
        this.legacyPaymentSuggestion.set(null);
      }'''
if old not in text:
    raise RuntimeError('Expected payment suggestion load block not found.')
path.write_text(text.replace(old, new, 1), encoding='utf-8')
print('Skipped advisory payment lookup when an existing fiscal document is being loaded.')
