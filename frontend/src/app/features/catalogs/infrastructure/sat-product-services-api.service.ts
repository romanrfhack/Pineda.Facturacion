import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import { SatProductServiceSearchItem } from '../models/catalogs.models';

@Injectable({ providedIn: 'root' })
export class SatProductServicesApiService {
  private readonly http = inject(HttpClient);

  search(query: string, take = 10): Observable<SatProductServiceSearchItem[]> {
    return this.http.get<SatProductServiceSearchItem[]>(
      buildApiUrl(`/fiscal/sat/product-services/search?q=${encodeURIComponent(query)}&take=${take}`)
    );
  }
}
