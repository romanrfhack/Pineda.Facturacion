import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { FiscalReceiverSatCatalog } from '../models/catalogs.models';
import { FiscalReceiversApiService } from '../infrastructure/fiscal-receivers-api.service';

@Injectable({ providedIn: 'root' })
export class FiscalReceiverSatCatalogService {
  private readonly api = inject(FiscalReceiversApiService);

  private readonly catalog$ = this.api.getSatCatalog().pipe(shareReplay(1));

  getCatalog(): Observable<FiscalReceiverSatCatalog> {
    return this.catalog$;
  }
}
