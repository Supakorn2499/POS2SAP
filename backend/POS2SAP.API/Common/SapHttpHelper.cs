namespace POS2SAP.API.Common;

public static class SapHttpHelper
{
  public const int DefaultTimeoutSeconds = 90;
  public const int MaxTimeoutSeconds     = 300;
  public const int MinTimeoutSeconds     = 10;

  public static int GetTimeoutSeconds(IReadOnlyDictionary<string, string> config)
  {
    if (int.TryParse(config.GetValueOrDefault(gbVar.CfgSapHttpTimeoutSeconds, DefaultTimeoutSeconds.ToString()), out var s))
      return Math.Clamp(s, MinTimeoutSeconds, MaxTimeoutSeconds);
    return DefaultTimeoutSeconds;
  }

  public static TimeSpan GetTimeout(IReadOnlyDictionary<string, string> config)
    => TimeSpan.FromSeconds(GetTimeoutSeconds(config));

  /// <summary>
  /// Per-request timeout for shared HttpClient instances (do not mutate client.Timeout after first send).
  /// </summary>
  public static CancellationTokenSource CreateRequestTimeoutCancellation(
    IReadOnlyDictionary<string, string> config,
    CancellationToken cancellationToken = default)
  {
    var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(GetTimeout(config));
    return cts;
  }
}
