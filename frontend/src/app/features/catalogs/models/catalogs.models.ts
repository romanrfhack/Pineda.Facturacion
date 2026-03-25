export interface MutationResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  id?: number | null;
}

export interface IssuerProfile {
  id: number;
  legalName: string;
  rfc: string;
  fiscalRegimeCode: string;
  postalCode: string;
  cfdiVersion: string;
  hasCertificateReference: boolean;
  hasPrivateKeyReference: boolean;
  hasPrivateKeyPasswordReference: boolean;
  hasLogo: boolean;
  logoFileName?: string | null;
  logoUpdatedAtUtc?: string | null;
  pacEnvironment: string;
  fiscalSeries?: string | null;
  nextFiscalFolio?: number | null;
  lastUsedFiscalFolio?: number | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpsertIssuerProfileRequest {
  legalName: string;
  rfc: string;
  fiscalRegimeCode: string;
  postalCode: string;
  cfdiVersion: string;
  certificateReference: string;
  privateKeyReference: string;
  privateKeyPasswordReference: string;
  pacEnvironment: string;
  fiscalSeries?: string | null;
  nextFiscalFolio: number | null;
  isActive: boolean;
}

export interface FiscalReceiverSearchItem {
  id: number;
  rfc: string;
  legalName: string;
  postalCode: string;
  fiscalRegimeCode: string;
  cfdiUseCodeDefault: string;
  isActive: boolean;
}

export interface FiscalReceiver extends FiscalReceiverSearchItem {
  countryCode?: string | null;
  foreignTaxRegistration?: string | null;
  email?: string | null;
  phone?: string | null;
  searchAlias?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpsertFiscalReceiverRequest {
  rfc: string;
  legalName: string;
  fiscalRegimeCode: string;
  cfdiUseCodeDefault: string;
  postalCode: string;
  countryCode?: string | null;
  foreignTaxRegistration?: string | null;
  email?: string | null;
  phone?: string | null;
  searchAlias?: string | null;
  isActive: boolean;
}

export interface ProductFiscalProfileSearchItem {
  id: number;
  internalCode: string;
  description: string;
  satProductServiceCode: string;
  satUnitCode: string;
  taxObjectCode: string;
  vatRate: number;
  isActive: boolean;
}

export interface ProductFiscalProfile extends ProductFiscalProfileSearchItem {
  defaultUnitText?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpsertProductFiscalProfileRequest {
  internalCode: string;
  description: string;
  satProductServiceCode: string;
  satUnitCode: string;
  taxObjectCode: string;
  vatRate: number;
  defaultUnitText?: string | null;
  isActive: boolean;
}

export interface ImportBatchSummary {
  batchId?: number | null;
  sourceFileName: string;
  status: string;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  ignoredRows: number;
  existingMasterMatches: number;
  duplicateRowsInFile: number;
  appliedRows: number;
  applyFailedRows: number;
  applySkippedRows: number;
  completedAtUtc?: string | null;
  lastAppliedAtUtc?: string | null;
  errorMessage?: string | null;
}

export interface ReceiverImportRow {
  rowNumber: number;
  status: string;
  suggestedAction: string;
  normalizedRfc?: string | null;
  normalizedLegalName?: string | null;
  normalizedCfdiUseCodeDefault?: string | null;
  normalizedFiscalRegimeCode?: string | null;
  normalizedPostalCode?: string | null;
  validationErrors: string[];
  existingMasterEntityId?: number | null;
  applyStatus: string;
  appliedAtUtc?: string | null;
  applyErrorMessage?: string | null;
  appliedMasterEntityId?: number | null;
}

export interface ProductImportRow {
  rowNumber: number;
  status: string;
  suggestedAction: string;
  normalizedInternalCode?: string | null;
  normalizedDescription?: string | null;
  normalizedSatProductServiceCode?: string | null;
  normalizedSatUnitCode?: string | null;
  normalizedTaxObjectCode?: string | null;
  normalizedVatRate?: number | null;
  validationErrors: string[];
  existingMasterEntityId?: number | null;
  applyStatus: string;
  appliedAtUtc?: string | null;
  applyErrorMessage?: string | null;
  appliedMasterEntityId?: number | null;
}

export type ImportApplyMode = 'CreateOnly' | 'CreateAndUpdate';

export interface ApplyImportBatchRequest {
  applyMode: ImportApplyMode;
  selectedRowNumbers?: number[] | null;
  stopOnFirstError: boolean;
}

export interface ApplyImportBatchRow {
  rowNumber: number;
  effectiveAction: string;
  applyStatus: string;
  appliedMasterEntityId?: number | null;
  errorMessage?: string | null;
}

export interface ApplyImportBatchResponse {
  batchId: number;
  applyMode: string;
  totalCandidateRows: number;
  appliedRows: number;
  skippedRows: number;
  failedRows: number;
  alreadyAppliedRows: number;
  lastAppliedAtUtc?: string | null;
  errorMessage?: string | null;
  rows: ApplyImportBatchRow[];
}
