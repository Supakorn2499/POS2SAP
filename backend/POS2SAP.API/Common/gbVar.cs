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

    // Config keys
    public const string CfgSapUrlTest              = "sap_url_test";
    public const string CfgSapUrlProd              = "sap_url_prod";
    public const string CfgSapEnv                  = "sap_env";
    public const string CfgSapAuthType             = "sap_auth_type";
    public const string CfgSapApiKey               = "sap_api_key";
    public const string CfgSapBasicUsername        = "sap_basic_username";
    public const string CfgSapBasicPassword        = "sap_basic_password";
    public const string CfgScheduleIntervalMinutes = "schedule_interval_minutes";
    public const string CfgScheduleEnabled         = "schedule_enabled";
    public const string CfgMaxRetryCount           = "max_retry_count";
}
