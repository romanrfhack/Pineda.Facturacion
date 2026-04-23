import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, Observable } from 'rxjs';
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

  searchPaged(query: string, page = 1, pageSize = 12): Observable<SatCatalogSearchPageResponse> {
    return this.http.get<SatCatalogSearchPageResponse>(
      buildApiUrl(
        `/fiscal/sat/product-services/search-paged?q=${encodeURIComponent(query)}&page=${page}&pageSize=${pageSize}`,
      ),
    );
  }

  searchBestEffort(query: string, pageSize = 12): Observable<SatProductServiceSearchItem[]> {
    return this.searchPaged(query, 1, pageSize).pipe(
      map((response) => response.items ?? []),
      catchError(() => this.search(query, pageSize)),
    );
  }
}

export interface SatCatalogSearchPageResponse {
  page: number;
  pageSize: number;
  hasMore: boolean;
  items: SatProductServiceSearchItem[];
}
