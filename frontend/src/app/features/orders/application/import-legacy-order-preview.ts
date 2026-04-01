import { ImportLegacyOrderPreviewResponse } from '../models/orders.models';

export interface ImportLegacyOrderPreviewViewModel {
  readonly legacyOrderId: string;
  readonly hasChanges: boolean;
  readonly changedOrderFields: string[];
  readonly addedLines: number;
  readonly removedLines: number;
  readonly modifiedLines: number;
  readonly unchangedLines: number;
  readonly oldTotal: number;
  readonly newTotal: number;
  readonly oldSubtotal: number;
  readonly newSubtotal: number;
  readonly eligibilityStatus: string;
  readonly eligibilityReasonCode: string;
  readonly eligibilityReasonMessage: string;
  readonly lineChanges: ImportLegacyOrderPreviewResponse['lineChanges'];
  readonly allowedActions: ImportLegacyOrderPreviewResponse['allowedActions'];
}

export function adaptImportLegacyOrderPreview(response: ImportLegacyOrderPreviewResponse): ImportLegacyOrderPreviewViewModel {
  return {
    legacyOrderId: response.legacyOrderId,
    hasChanges: response.hasChanges,
    changedOrderFields: response.changedOrderFields,
    addedLines: response.changeSummary.addedLines,
    removedLines: response.changeSummary.removedLines,
    modifiedLines: response.changeSummary.modifiedLines,
    unchangedLines: response.changeSummary.unchangedLines,
    oldTotal: response.changeSummary.oldTotal,
    newTotal: response.changeSummary.newTotal,
    oldSubtotal: response.changeSummary.oldSubtotal,
    newSubtotal: response.changeSummary.newSubtotal,
    eligibilityStatus: response.reimportEligibility.status,
    eligibilityReasonCode: response.reimportEligibility.reasonCode,
    eligibilityReasonMessage: response.reimportEligibility.reasonMessage,
    lineChanges: response.lineChanges,
    allowedActions: response.allowedActions
  };
}
