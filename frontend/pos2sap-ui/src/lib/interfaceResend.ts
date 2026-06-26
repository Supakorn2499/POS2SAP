import interfaceService from '@/services/interfaceService';

/** Map DB interface_type (AR/AP/DL) to trigger API name. */
export function interfaceTypeToTrigger(interfaceType: string): string {
  switch (interfaceType) {
    case 'AP': return 'IncomingPayment';
    case 'DL': return 'Delivery';
    default: return 'ARInvoice';
  }
}

export function interfaceTypeLabel(interfaceType: string | undefined, t: (key: string) => string): string {
  switch (interfaceType) {
    case 'AP': return t('interfaceTypeAP');
    case 'DL': return t('interfaceTypeDL');
    case 'AR':
    default: return t('interfaceTypeAR');
  }
}

/** Re-send a failed/retry log — AP uses trigger; AR/DL use retry endpoint. */
export async function resendInterfaceLog(
  id: string,
  posDocNo: string,
  interfaceType?: string,
): Promise<boolean> {
  if (interfaceType === 'AP') {
    const result = await interfaceService.triggerManualFor('IncomingPayment', [posDocNo]);
    return result.sent > 0;
  }
  return interfaceService.retryRecord(id);
}
