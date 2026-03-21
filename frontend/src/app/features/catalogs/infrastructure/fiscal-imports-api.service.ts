import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ApplyImportBatchRequest,
  ApplyImportBatchResponse,
  ImportBatchSummary,
  ProductImportRow,
  ReceiverImportRow
} from '../models/catalogs.models';
import { buildApiUrl } from '../../../core/config/api-url';

@Injectable({ providedIn: 'root' })
export class FiscalImportsApiService {
  private readonly http = inject(HttpClient);

  previewReceivers(file: File): Observable<ImportBatchSummary> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ImportBatchSummary>(buildApiUrl('/fiscal/imports/receivers/preview'), form);
  }

  getReceiverBatch(batchId: number): Observable<ImportBatchSummary> {
    return this.http.get<ImportBatchSummary>(buildApiUrl(`/fiscal/imports/receivers/batches/${batchId}`));
  }

  listReceiverRows(batchId: number): Observable<ReceiverImportRow[]> {
    return this.http.get<ReceiverImportRow[]>(buildApiUrl(`/fiscal/imports/receivers/batches/${batchId}/rows`));
  }

  applyReceiverBatch(batchId: number, request: ApplyImportBatchRequest): Observable<ApplyImportBatchResponse> {
    return this.http.post<ApplyImportBatchResponse>(buildApiUrl(`/fiscal/imports/receivers/batches/${batchId}/apply`), mapApplyRequest(request));
  }

  previewProducts(file: File, defaults: { defaultTaxObjectCode?: string; defaultVatRate?: number | null; defaultUnitText?: string }): Observable<ImportBatchSummary> {
    const form = new FormData();
    form.append('file', file);
    if (defaults.defaultTaxObjectCode) {
      form.append('defaultTaxObjectCode', defaults.defaultTaxObjectCode);
    }
    if (defaults.defaultVatRate != null) {
      form.append('defaultVatRate', String(defaults.defaultVatRate));
    }
    if (defaults.defaultUnitText) {
      form.append('defaultUnitText', defaults.defaultUnitText);
    }
    return this.http.post<ImportBatchSummary>(buildApiUrl('/fiscal/imports/products/preview'), form);
  }

  getProductBatch(batchId: number): Observable<ImportBatchSummary> {
    return this.http.get<ImportBatchSummary>(buildApiUrl(`/fiscal/imports/products/batches/${batchId}`));
  }

  listProductRows(batchId: number): Observable<ProductImportRow[]> {
    return this.http.get<ProductImportRow[]>(buildApiUrl(`/fiscal/imports/products/batches/${batchId}/rows`));
  }

  applyProductBatch(batchId: number, request: ApplyImportBatchRequest): Observable<ApplyImportBatchResponse> {
    return this.http.post<ApplyImportBatchResponse>(buildApiUrl(`/fiscal/imports/products/batches/${batchId}/apply`), mapApplyRequest(request));
  }
}

function mapApplyRequest(request: ApplyImportBatchRequest): Record<string, unknown> {
  const payload: Record<string, unknown> = {
    applyMode: request.applyMode === 'CreateAndUpdate' ? 1 : 0,
    stopOnFirstError: request.stopOnFirstError
  };

  if (request.selectedRowNumbers && request.selectedRowNumbers.length) {
    payload['selectedRowNumbers'] = request.selectedRowNumbers;
  }

  return payload;
}
