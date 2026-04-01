import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { ExternalRepBaseDocumentImportCardComponent } from './external-rep-base-document-import-card.component';

describe('ExternalRepBaseDocumentImportCardComponent', () => {
  async function configure(apiOverrides?: Partial<Record<keyof PaymentComplementsApiService, unknown>>) {
    await TestBed.configureTestingModule({
      imports: [ExternalRepBaseDocumentImportCardComponent],
      providers: [
        {
          provide: PaymentComplementsApiService,
          useValue: {
            importExternalBaseDocumentXml: vi.fn().mockReturnValue(of({
              outcome: 'Accepted',
              isSuccess: true,
              externalRepBaseDocumentId: 321,
              validationStatus: 'Accepted',
              reasonCode: 'Accepted',
              reasonMessage: 'Factura externa importada correctamente.',
              uuid: 'UUID-EXT-1',
              issuerRfc: 'AAA010101AAA',
              receiverRfc: 'BBB010101BBB',
              paymentMethodSat: 'PPD',
              paymentFormSat: '99',
              currencyCode: 'MXN',
              total: 116,
              isDuplicate: false
            })),
            ...apiOverrides
          }
        },
        { provide: FeedbackService, useValue: { show: vi.fn() } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ExternalRepBaseDocumentImportCardComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the xml import form', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('Importar CFDI externo por XML');
    expect(fixture.nativeElement.textContent).toContain('Sin archivo seleccionado');
    expect(fixture.nativeElement.textContent).toContain('Importar XML');
  });

  it('imports an external xml successfully and renders the result summary', async () => {
    const importExternalBaseDocumentXml = vi.fn().mockReturnValue(of({
      outcome: 'Accepted',
      isSuccess: true,
      externalRepBaseDocumentId: 321,
      validationStatus: 'Accepted',
      reasonCode: 'Accepted',
      reasonMessage: 'Factura externa importada correctamente.',
      uuid: 'UUID-EXT-1',
      issuerRfc: 'AAA010101AAA',
      receiverRfc: 'BBB010101BBB',
      paymentMethodSat: 'PPD',
      paymentFormSat: '99',
      currencyCode: 'MXN',
      total: 116,
      isDuplicate: false
    }));
    const fixture = await configure({ importExternalBaseDocumentXml });
    const file = new File(['<cfdi:Comprobante />'], 'external.xml', { type: 'application/xml' });

    fixture.componentInstance['selectedFile'].set(file);
    fixture.componentInstance['selectedFileName'].set(file.name);
    await fixture.componentInstance['submit']();
    fixture.detectChanges();

    expect(importExternalBaseDocumentXml).toHaveBeenCalledWith(file);
    expect(fixture.nativeElement.textContent).toContain('Accepted');
    expect(fixture.nativeElement.textContent).toContain('UUID-EXT-1');
    expect(fixture.nativeElement.textContent).toContain('321');
  });

  it('renders duplicate or rejection errors clearly', async () => {
    const importExternalBaseDocumentXml = vi.fn().mockReturnValue(throwError(() => ({
      error: {
        outcome: 'Duplicate',
        isSuccess: false,
        externalRepBaseDocumentId: 654,
        validationStatus: 'Rejected',
        reasonCode: 'DuplicateExternalInvoice',
        reasonMessage: 'Ya existe una factura externa importada con el mismo UUID.',
        errorMessage: 'Ya existe una factura externa importada con el mismo UUID.',
        uuid: 'UUID-EXT-DUP',
        issuerRfc: 'AAA010101AAA',
        receiverRfc: 'BBB010101BBB',
        paymentMethodSat: 'PPD',
        paymentFormSat: '99',
        currencyCode: 'MXN',
        total: 116,
        isDuplicate: true
      }
    })));
    const fixture = await configure({ importExternalBaseDocumentXml });
    const file = new File(['<cfdi:Comprobante />'], 'duplicate.xml', { type: 'application/xml' });

    fixture.componentInstance['selectedFile'].set(file);
    fixture.componentInstance['selectedFileName'].set(file.name);
    await fixture.componentInstance['submit']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ya existe una factura externa importada con el mismo UUID.');
    expect(fixture.nativeElement.textContent).toContain('DuplicateExternalInvoice');
    expect(fixture.nativeElement.textContent).toContain('UUID-EXT-DUP');
    expect(fixture.nativeElement.textContent).toContain('654');
  });
});
