export type ImportApplySelectionMode = 'allEligible' | 'specificRows';

export interface ImportApplyRowLike {
  status: string;
  suggestedAction: string;
}

export interface SelectedRowsResolution {
  selectedRowNumbers?: number[];
  errorMessage?: string;
}

export function countEligibleImportRows(rows: ImportApplyRowLike[]): number {
  return rows.filter(isEligibleImportRow).length;
}

export function isEligibleImportRow(row: ImportApplyRowLike): boolean {
  return row.status === 'Valid' && (row.suggestedAction === 'Create' || row.suggestedAction === 'Update');
}

export function resolveSelectedRowNumbers(
  selectionMode: ImportApplySelectionMode,
  rawValue: string
): SelectedRowsResolution {
  if (selectionMode === 'allEligible') {
    return {};
  }

  const tokens = rawValue
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);

  if (!tokens.length) {
    return {
      errorMessage: 'Ingresa números de fila válidos separados por comas.'
    };
  }

  const values = tokens.map((token) => Number(token));
  const invalid = values.some((item) => !Number.isInteger(item) || item <= 0);
  if (invalid) {
    return {
      errorMessage: 'Ingresa números de fila válidos separados por comas.'
    };
  }

  return {
    selectedRowNumbers: Array.from(new Set(values)).sort((left, right) => left - right)
  };
}
