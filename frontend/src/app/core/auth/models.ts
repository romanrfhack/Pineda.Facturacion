export enum AppRole {
  Admin = 'Admin',
  FiscalSupervisor = 'FiscalSupervisor',
  FiscalOperator = 'FiscalOperator',
  Auditor = 'Auditor'
}

export interface CurrentUser {
  id: number | null;
  username: string | null;
  displayName: string | null;
  roles: AppRole[];
  isAuthenticated: boolean;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  token?: string | null;
  expiresAtUtc?: string | null;
  user?: CurrentUser | null;
}
