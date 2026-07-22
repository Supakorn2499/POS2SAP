namespace POS2SAP.API.Common;

public static class gbVar
{
    public static string MainConstr { get; set; } = string.Empty;

    // Interface Status constants
    public const string StatusPending    = "PENDING";
    public const string StatusProcessing = "PROCESSING";
    public const string StatusSuccess    = "SUCCESS";
    public const string StatusFailed     = "FAILED";
    public const string StatusRetry      = "RETRY";

    // SAP fixed values
    public const string SapDocCur     = "THB";
    public const string SapPymntGroup = "Cash";
    public const string SapVatGroup   = "S07";
    public const decimal SapVatPrcnt  = 7m;
    public const string SapOcrCode2   = "CENTER";

    // Delivery line — SAP spec uses 'CEN' (differs from AR Invoice)
    public const string SapDeliveryOcrCode2 = "CEN";

    // Config keys
    public const string CfgSapUrlTest              = "sap_url_test";
    public const string CfgSapUrlProd              = "sap_url_prod";
    public const string CfgSapEnv                  = "sap_env";
    public const string CfgSapAuthType             = "sap_auth_type";
    public const string CfgSapApiKey               = "sap_api_key";
    public const string CfgSapBasicUsername        = "sap_basic_username";
    public const string CfgSapBasicPassword        = "sap_basic_password";
    public const string CfgScheduleIntervalMinutes   = "schedule_interval_minutes";
    public const string CfgScheduleEnabled           = "schedule_enabled";
    public const string CfgScheduleWindowStart       = "schedule_window_start";
    public const string CfgScheduleWindowEnd         = "schedule_window_end";
    public const string CfgScheduleTimezone          = "schedule_timezone";
    public const string CfgScheduleMaxRuntimeMinutes = "schedule_max_runtime_minutes";
    public const string CfgInterfaceCutoverDate      = "interface_cutover_date";
    public const string CfgImportDateToMode          = "import_date_to_mode";
    public const string CfgMaxRetryCount             = "max_retry_count";
    public const string CfgImportBatchSize           = "import_batch_size";
    public const string CfgSapHttpTimeoutSeconds     = "sap_http_timeout_seconds";
    public const string CfgImportChunkDays           = "import_chunk_days";

    // Product group mapping placeholder
    public const string SapItemGroupPending        = "[SAP-PENDING]";

    // Incoming Payment — paytype_gl_mapping.SapPayCategory
    public const string SapPayCategoryCash        = "CASH";
    public const string SapPayCategoryTransfer    = "TRANSFER";
    public const string SapPayCategoryCreditCard  = "CREDIT_CARD";
    public const string SapPayCategorySkip        = "SKIP";

    // AR Invoice — synthetic negative discount / promo lines
    public const string SapArItemCoupon       = "RV-DC-0001";
    public const string SapArCatCoupon        = "DC";
    public const string SapArItemGiftVoucher  = "RV-CP-0001";
    public const string SapArCatGiftVoucher   = "GV";
    public const string SapArItemFreebie      = "";
    public const string SapArCatFreebie       = "OP";
    public const string SapArItemRedeem       = "RV-RD-0001";
    public const string SapArCatRedeem        = "RD";
    public const string SapArItemServiceCharge = "SC";
    public const string SapArCatServiceCharge   = "SC";
}
