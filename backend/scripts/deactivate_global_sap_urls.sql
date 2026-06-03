UPDATE interface_configs
SET is_active = 0, updated_at = GETUTCDATE()
WHERE config_key IN ('sap_url_test','sap_url_prod') AND is_active = 1;
GO
