export interface AuditEventItem {
  id: number;
  occurredAtUtc: string;
  actorUserId?: number | null;
  actorUsername?: string | null;
  actionType: string;
  entityType: string;
  entityId?: string | null;
  outcome: string;
  correlationId: string;
  requestSummaryJson?: string | null;
  responseSummaryJson?: string | null;
  errorMessage?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAtUtc: string;
}

export interface AuditEventListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  items: AuditEventItem[];
}

export interface AuditEventFilters {
  page: number;
  pageSize: number;
  actorUsername?: string;
  actionType?: string;
  entityType?: string;
  entityId?: string;
  outcome?: string;
  fromUtc?: string;
  toUtc?: string;
  correlationId?: string;
}
