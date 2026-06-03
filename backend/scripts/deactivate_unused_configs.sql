SET NOCOUNT ON;
UPDATE interface_configs
SET is_active = 0,
    updated_at = GETUTCDATE()
WHERE config_key IN ('ARInvoice.AR','IncomingPayment.IC');

SELECT config_key, is_active, updated_at
FROM interface_configs
WHERE config_key IN ('ARInvoice.AR','IncomingPayment.IC');
