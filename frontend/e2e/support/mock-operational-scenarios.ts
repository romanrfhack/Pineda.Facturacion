import type { Page } from '@playwright/test';

type Role = 'Admin' | 'FiscalSupervisor' | 'FiscalOperator' | 'Auditor';

interface SessionOptions {
  username: string;
  displayName: string;
  role: Role;
}

export async function mockInvoiceStampingJourney(page: Page): Promise<void> {
  let fiscalDocumentStatus = 'ReadyForStamping';
  let stampEvidence: Record<string, unknown> | null = null;

  await mockSession(page, {
    username: 'supervisor',
    displayName: 'Supervisor',
    role: 'FiscalSupervisor'
  });

  await mockIssuerAndReceiverLookup(page);

  await page.route('**/api/orders/LEG-7101/import', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Imported',
        isSuccess: true,
        isIdempotent: false,
        sourceSystem: 'legacy',
        sourceTable: 'pedidos',
        legacyOrderId: 'LEG-7101',
        sourceHash: 'hash-7101',
        legacyImportRecordId: 101,
        salesOrderId: 201,
        importStatus: 'Imported'
      }
    });
  });

  await page.route('**/api/sales-orders/201/billing-documents', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        salesOrderId: 201,
        billingDocumentId: 301,
        billingDocumentStatus: 'Draft'
      }
    });
  });

  await page.route('**/api/billing-documents/301', async (route) => {
    await route.fulfill({
      json: {
        billingDocumentId: 301,
        salesOrderId: 201,
        legacyOrderId: 'LEG-7101',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 100,
        createdAtUtc: '2026-03-20T12:00:00Z',
        fiscalDocumentId: fiscalDocumentStatus === 'ReadyForStamping' ? null : 401,
        fiscalDocumentStatus: fiscalDocumentStatus === 'ReadyForStamping' ? null : fiscalDocumentStatus
      }
    });
  });

  await page.route('**/api/billing-documents/301/fiscal-documents', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        billingDocumentId: 301,
        fiscalDocumentId: 401,
        status: 'ReadyForStamping'
      }
    });
  });

  await page.route('**/api/fiscal-documents/401', async (route) => {
    await route.fulfill({ json: buildFiscalDocument(401, fiscalDocumentStatus) });
  });

  await page.route('**/api/fiscal-documents/401/stamp', async (route) => {
    if (route.request().method() === 'GET') {
      if (!stampEvidence) {
        await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
        return;
      }

      await route.fulfill({ json: stampEvidence });
      return;
    }

    fiscalDocumentStatus = 'Stamped';
    stampEvidence = buildStampEvidence({
      fiscalDocumentId: 401,
      uuid: 'UUID-FISCAL-7101',
      stampedAtUtc: '2026-03-20T13:00:00Z'
    });

    await route.fulfill({
      json: {
        outcome: 'Stamped',
        isSuccess: true,
        fiscalDocumentId: 401,
        fiscalDocumentStatus: 'Stamped',
        fiscalStampId: 501,
        uuid: 'UUID-FISCAL-7101',
        stampedAtUtc: '2026-03-20T13:00:00Z',
        providerName: 'FacturaloPlus',
        providerTrackingId: 'TRACK-FISCAL-7101'
      }
    });
  });

  await page.route('**/api/fiscal-documents/401/cancellation', async (route) => {
    await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
  });
}

export async function mockAccountsReceivableJourney(page: Page): Promise<void> {
  let invoice = createInvoice({
    id: 601,
    fiscalDocumentId: 401,
    status: 'Open',
    paidTotal: 0,
    outstandingBalance: 100,
    applications: []
  });
  let invoiceExists = false;

  let payment: Record<string, unknown> | null = null;

  await mockSession(page, {
    username: 'operator',
    displayName: 'Operador',
    role: 'FiscalOperator'
  });

  await page.route('**/api/billing-documents/301', async (route) => {
    await route.fulfill({
      json: {
        billingDocumentId: 301,
        salesOrderId: 201,
        legacyOrderId: 'LEG-7101',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 100,
        createdAtUtc: '2026-03-20T12:00:00Z',
        fiscalDocumentId: 401,
        fiscalDocumentStatus: 'Stamped'
      }
    });
  });

  await page.route('**/api/fiscal-documents/401/accounts-receivable', async (route) => {
    if (route.request().method() === 'GET') {
      if (!invoiceExists) {
        await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
        return;
      }

      await route.fulfill({ json: invoice });
      return;
    }

    invoiceExists = true;
    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        fiscalDocumentId: 401,
        accountsReceivableInvoice: invoice
      }
    });
  });

  await page.route('**/api/accounts-receivable/payments', async (route) => {
    payment = createPayment({
      id: 701,
      amount: 40,
      appliedTotal: 0,
      remainingAmount: 40,
      applications: []
    });

    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        payment
      }
    });
  });

  await page.route('**/api/accounts-receivable/payments/701', async (route) => {
    if (!payment) {
      await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
      return;
    }

    await route.fulfill({ json: payment });
  });

  await page.route('**/api/accounts-receivable/payments/701/apply', async (route) => {
    const application = {
      id: 801,
      accountsReceivablePaymentId: 701,
      accountsReceivableInvoiceId: 601,
      applicationSequence: 1,
      appliedAmount: 40,
      previousBalance: 100,
      newBalance: 60,
      createdAtUtc: '2026-03-20T14:05:00Z'
    };

    invoice = createInvoice({
      id: 601,
      fiscalDocumentId: 401,
      status: 'PartiallyPaid',
      paidTotal: 40,
      outstandingBalance: 60,
      applications: [application]
    });
    payment = createPayment({
      id: 701,
      amount: 40,
      appliedTotal: 40,
      remainingAmount: 0,
      applications: [application]
    });

    await route.fulfill({
      json: {
        outcome: 'Applied',
        isSuccess: true,
        accountsReceivablePaymentId: 701,
        appliedCount: 1,
        remainingPaymentAmount: 0,
        payment,
        applications: [application]
      }
    });
  });
}

export async function mockPaymentComplementJourney(page: Page): Promise<void> {
  let complementStatus = 'ReadyForStamping';
  let complementExists = false;
  let stampEvidence: Record<string, unknown> | null = null;

  await mockSession(page, {
    username: 'supervisor',
    displayName: 'Supervisor',
    role: 'FiscalSupervisor'
  });

  await page.route('**/api/billing-documents/301', async (route) => {
    await route.fulfill({
      json: {
        billingDocumentId: 301,
        salesOrderId: 201,
        legacyOrderId: 'LEG-7101',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 100,
        createdAtUtc: '2026-03-20T12:00:00Z',
        fiscalDocumentId: 401,
        fiscalDocumentStatus: 'Stamped'
      }
    });
  });

  await page.route('**/api/accounts-receivable/payments/702/payment-complement', async (route) => {
    if (!complementExists) {
      await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
      return;
    }

    await route.fulfill({
      json: buildPaymentComplementDocument(802, complementStatus)
    });
  });

  await page.route('**/api/accounts-receivable/payments/702/payment-complements', async (route) => {
    complementExists = true;
    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        accountsReceivablePaymentId: 702,
        paymentComplementId: 802,
        status: 'ReadyForStamping'
      }
    });
  });

  await page.route('**/api/payment-complements/802/stamp', async (route) => {
    if (route.request().method() === 'GET') {
      if (!stampEvidence) {
        await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
        return;
      }

      await route.fulfill({ json: stampEvidence });
      return;
    }

    complementStatus = 'Stamped';
    stampEvidence = buildPaymentComplementStampEvidence({
      paymentComplementDocumentId: 802,
      uuid: 'UUID-PC-702',
      stampedAtUtc: '2026-03-20T15:00:00Z'
    });

    await route.fulfill({
      json: {
        outcome: 'Stamped',
        isSuccess: true,
        paymentComplementId: 802,
        status: 'Stamped',
        paymentComplementStampId: 902,
        uuid: 'UUID-PC-702',
        stampedAtUtc: '2026-03-20T15:00:00Z',
        providerName: 'FacturaloPlus',
        providerTrackingId: 'TRACK-PC-702'
      }
    });
  });

  await page.route('**/api/payment-complements/802/cancellation', async (route) => {
    await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
  });
}

export async function mockMixedReceiversComplementFailure(page: Page): Promise<void> {
  await mockSession(page, {
    username: 'supervisor',
    displayName: 'Supervisor',
    role: 'FiscalSupervisor'
  });

  await page.route('**/api/accounts-receivable/payments/703/payment-complement', async (route) => {
    await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
  });

  await page.route('**/api/accounts-receivable/payments/703/payment-complements', async (route) => {
    await route.fulfill({
      status: 400,
      json: {
        outcome: 'ValidationFailed',
        isSuccess: false,
        errorMessage: 'Applied invoices belong to different receivers.'
      }
    });
  });
}

export async function mockOperatorReadOnlyFiscalDocument(page: Page): Promise<void> {
  await mockSession(page, {
    username: 'operator',
    displayName: 'Operador',
    role: 'FiscalOperator'
  });

  await mockIssuerAndReceiverLookup(page);
  await page.route('**/api/billing-documents/301', async (route) => {
    await route.fulfill({
      json: {
        billingDocumentId: 301,
        salesOrderId: 201,
        legacyOrderId: 'LEG-7101',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 100,
        createdAtUtc: '2026-03-20T12:00:00Z',
        fiscalDocumentId: 405,
        fiscalDocumentStatus: 'Stamped'
      }
    });
  });
  await page.route('**/api/fiscal-documents/405', async (route) => {
    await route.fulfill({ json: buildFiscalDocument(405, 'Stamped') });
  });
  await page.route('**/api/fiscal-documents/405/stamp', async (route) => {
    await route.fulfill({
      json: buildStampEvidence({
        fiscalDocumentId: 405,
        uuid: 'UUID-FISCAL-RO',
        stampedAtUtc: '2026-03-20T16:00:00Z'
      })
    });
  });
  await page.route('**/api/fiscal-documents/405/cancellation', async (route) => {
    await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
  });
}

export async function mockStampUnavailableFiscalDocument(page: Page): Promise<void> {
  await mockSession(page, {
    username: 'supervisor',
    displayName: 'Supervisor',
    role: 'FiscalSupervisor'
  });

  await mockIssuerAndReceiverLookup(page);
  await page.route('**/api/billing-documents/301', async (route) => {
    await route.fulfill({
      json: {
        billingDocumentId: 301,
        salesOrderId: 201,
        legacyOrderId: 'LEG-7101',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 100,
        createdAtUtc: '2026-03-20T12:00:00Z',
        fiscalDocumentId: 406,
        fiscalDocumentStatus: 'ReadyForStamping'
      }
    });
  });
  await page.route('**/api/fiscal-documents/406', async (route) => {
    await route.fulfill({ json: buildFiscalDocument(406, 'ReadyForStamping') });
  });
  await page.route('**/api/fiscal-documents/406/stamp', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
      return;
    }

    await route.fulfill({
      status: 503,
      json: {
        outcome: 'ProviderUnavailable',
        isSuccess: false,
        errorMessage: 'PAC provider is unavailable. Retry after checking status.',
        fiscalDocumentId: 406,
        fiscalDocumentStatus: 'ReadyForStamping'
      }
    });
  });
  await page.route('**/api/fiscal-documents/406/cancellation', async (route) => {
    await route.fulfill({ status: 404, json: { errorMessage: 'No encontrado.' } });
  });
}

export async function mockSession(page: Page, options: SessionOptions): Promise<void> {
  const user = {
    id: 1,
    username: options.username,
    displayName: options.displayName,
    roles: [options.role],
    isAuthenticated: true
  };

  await page.route('**/api/auth/login', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Authenticated',
        isSuccess: true,
        token: `${options.username}-token`,
        expiresAtUtc: new Date().toISOString(),
        user
      }
    });
  });

  await page.route('**/api/auth/me', async (route) => {
    await route.fulfill({ json: user });
  });
}

async function mockIssuerAndReceiverLookup(page: Page): Promise<void> {
  await page.route('**/api/fiscal/issuer-profile/active', async (route) => {
    await route.fulfill({
      json: {
        id: 1,
        legalName: 'Issuer SA',
        rfc: 'AAA010101AAA',
        fiscalRegimeCode: '601',
        postalCode: '01000',
        cfdiVersion: '4.0',
        hasCertificateReference: true,
        hasPrivateKeyReference: true,
        hasPrivateKeyPasswordReference: true,
        pacEnvironment: 'Sandbox',
        isActive: true
      }
    });
  });

  await page.route('**/api/fiscal/receivers/search**', async (route) => {
    await route.fulfill({
      json: [
        {
          id: 9,
          rfc: 'BBB010101BBB',
          legalName: 'Receiver One',
          postalCode: '02000',
          fiscalRegimeCode: '601',
          cfdiUseCodeDefault: 'G03',
          isActive: true
        }
      ]
    });
  });
}

function buildFiscalDocument(id: number, status: string): Record<string, unknown> {
  return {
    id,
    billingDocumentId: 301,
    issuerProfileId: 1,
    fiscalReceiverId: 9,
    status,
    cfdiVersion: '4.0',
    documentType: 'I',
    issuedAtUtc: '2026-03-20T12:00:00Z',
    currencyCode: 'MXN',
    exchangeRate: 1,
    paymentMethodSat: 'PPD',
    paymentFormSat: '99',
    paymentCondition: 'CREDITO',
    isCreditSale: true,
    creditDays: 7,
    issuerRfc: 'AAA010101AAA',
    issuerLegalName: 'Issuer SA',
    issuerFiscalRegimeCode: '601',
    issuerPostalCode: '01000',
    pacEnvironment: 'Sandbox',
    hasCertificateReference: true,
    hasPrivateKeyReference: true,
    hasPrivateKeyPasswordReference: true,
    receiverRfc: 'BBB010101BBB',
    receiverLegalName: 'Receiver One',
    receiverFiscalRegimeCode: '601',
    receiverCfdiUseCode: 'G03',
    receiverPostalCode: '02000',
    receiverCountryCode: 'MX',
    receiverForeignTaxRegistration: null,
    subtotal: 100,
    discountTotal: 0,
    taxTotal: 0,
    total: 100,
    items: [
      {
        id: 400,
        fiscalDocumentId: id,
        lineNumber: 1,
        billingDocumentItemId: 300,
        internalCode: 'SKU-1',
        description: 'Product SKU-1',
        quantity: 1,
        unitPrice: 100,
        discountAmount: 0,
        subtotal: 100,
        taxTotal: 0,
        total: 100,
        satProductServiceCode: '01010101',
        satUnitCode: 'H87',
        taxObjectCode: '02',
        vatRate: 0,
        unitText: 'Pieza'
      }
    ]
  };
}

function buildStampEvidence(input: { fiscalDocumentId: number; uuid: string; stampedAtUtc: string }): Record<string, unknown> {
  return {
    id: 501,
    fiscalDocumentId: input.fiscalDocumentId,
    providerName: 'FacturaloPlus',
    providerOperation: 'stamp',
    providerTrackingId: 'TRACK-FISCAL-7101',
    status: 'Stamped',
    uuid: input.uuid,
    stampedAtUtc: input.stampedAtUtc,
    providerCode: '200',
    providerMessage: 'Timbrado correctamente',
    errorCode: null,
    errorMessage: null,
    xmlHash: 'XML-HASH-FISCAL',
    qrCodeTextOrUrl: 'https://sat.example/qr/fiscal',
    originalString: `||1.1|${input.uuid}||`,
    createdAtUtc: input.stampedAtUtc,
    updatedAtUtc: input.stampedAtUtc
  };
}

function createInvoice(input: {
  id: number;
  fiscalDocumentId: number;
  status: string;
  paidTotal: number;
  outstandingBalance: number;
  applications: Record<string, unknown>[];
}): Record<string, unknown> {
  return {
    id: input.id,
    billingDocumentId: 301,
    fiscalDocumentId: input.fiscalDocumentId,
    fiscalStampId: 501,
    status: input.status,
    paymentMethodSat: 'PPD',
    paymentFormSatInitial: '99',
    isCreditSale: true,
    creditDays: 7,
    issuedAtUtc: '2026-03-20T12:00:00Z',
    dueAtUtc: '2026-03-27T12:00:00Z',
    currencyCode: 'MXN',
    total: 100,
    paidTotal: input.paidTotal,
    outstandingBalance: input.outstandingBalance,
    createdAtUtc: '2026-03-20T12:30:00Z',
    updatedAtUtc: '2026-03-20T14:05:00Z',
    applications: input.applications
  };
}

function createPayment(input: {
  id: number;
  amount: number;
  appliedTotal: number;
  remainingAmount: number;
  applications: Record<string, unknown>[];
}): Record<string, unknown> {
  return {
    id: input.id,
    paymentDateUtc: '2026-03-20T14:00:00Z',
    paymentFormSat: '03',
    currencyCode: 'MXN',
    amount: input.amount,
    appliedTotal: input.appliedTotal,
    remainingAmount: input.remainingAmount,
    reference: 'PAY-701',
    notes: 'Partial payment',
    receivedFromFiscalReceiverId: 9,
    createdAtUtc: '2026-03-20T14:00:00Z',
    updatedAtUtc: '2026-03-20T14:05:00Z',
    applications: input.applications
  };
}

function buildPaymentComplementDocument(id: number, status: string): Record<string, unknown> {
  return {
    id,
    accountsReceivablePaymentId: 702,
    status,
    providerName: 'FacturaloPlus',
    cfdiVersion: '4.0',
    documentType: 'P',
    issuedAtUtc: '2026-03-20T15:00:00Z',
    paymentDateUtc: '2026-03-20T14:00:00Z',
    currencyCode: 'MXN',
    totalPaymentsAmount: 100,
    issuerProfileId: 1,
    fiscalReceiverId: 9,
    issuerRfc: 'AAA010101AAA',
    issuerLegalName: 'Issuer SA',
    issuerFiscalRegimeCode: '601',
    issuerPostalCode: '01000',
    receiverRfc: 'BBB010101BBB',
    receiverLegalName: 'Receiver One',
    receiverFiscalRegimeCode: '601',
    receiverPostalCode: '02000',
    receiverCountryCode: 'MX',
    receiverForeignTaxRegistration: null,
    pacEnvironment: 'Sandbox',
    hasCertificateReference: true,
    hasPrivateKeyReference: true,
    hasPrivateKeyPasswordReference: true,
    relatedDocuments: [
      {
        id: 811,
        accountsReceivableInvoiceId: 611,
        fiscalDocumentId: 411,
        fiscalStampId: 511,
        relatedDocumentUuid: 'UUID-FISCAL-411',
        installmentNumber: 1,
        previousBalance: 100,
        paidAmount: 100,
        remainingBalance: 0,
        currencyCode: 'MXN',
        createdAtUtc: '2026-03-20T14:10:00Z'
      }
    ]
  };
}

function buildPaymentComplementStampEvidence(input: {
  paymentComplementDocumentId: number;
  uuid: string;
  stampedAtUtc: string;
}): Record<string, unknown> {
  return {
    id: 902,
    paymentComplementDocumentId: input.paymentComplementDocumentId,
    providerName: 'FacturaloPlus',
    providerOperation: 'payment-complement-stamp',
    providerTrackingId: 'TRACK-PC-702',
    status: 'Stamped',
    uuid: input.uuid,
    stampedAtUtc: input.stampedAtUtc,
    providerCode: '200',
    providerMessage: 'Timbrado correctamente',
    errorCode: null,
    errorMessage: null,
    xmlHash: 'XML-HASH-PC',
    qrCodeTextOrUrl: 'https://sat.example/qr/payment-complement',
    originalString: `||1.1|${input.uuid}||`,
    createdAtUtc: input.stampedAtUtc,
    updatedAtUtc: input.stampedAtUtc
  };
}
