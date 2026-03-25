import { buildFiscalDocumentFileName } from './fiscal-document-file-name';

describe('buildFiscalDocumentFileName', () => {
  it('builds the file name without series', () => {
    expect(buildFiscalDocumentFileName({
      issuerRfc: 'ARP9706105W2',
      folio: '31786',
      receiverRfc: 'HOL990616BP1',
      fallbackToken: 'UUID-1'
    }, 'pdf')).toBe('ARP9706105W2_31786_HOL990616BP1.pdf');
  });

  it('builds the file name with series concatenated to folio', () => {
    expect(buildFiscalDocumentFileName({
      issuerRfc: 'ARP9706105W2',
      series: 'A',
      folio: '31786',
      receiverRfc: 'HOL990616BP1',
      fallbackToken: 'UUID-1'
    }, 'xml')).toBe('ARP9706105W2_A31786_HOL990616BP1.xml');
  });

  it('falls back and sanitizes only problematic characters', () => {
    expect(buildFiscalDocumentFileName({
      issuerRfc: ' ARP9706105W2 ',
      receiverRfc: ' HOL990616BP1 ',
      fallbackToken: ' UUID:123 '
    }, '.pdf')).toBe('ARP9706105W2_UUID_123_HOL990616BP1.pdf');
  });
});
