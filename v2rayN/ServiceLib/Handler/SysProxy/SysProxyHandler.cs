namespace ServiceLib.Handler.SysProxy;

public static class SysProxyHandler
{
    private static readonly string _tag = "SysProxyHandler";

    public static async Task<bool> UpdateSysProxy(Config config, bool forceDisable)
    {
        var type = config.SystemProxyItem.SysProxyType;

        if (forceDisable && type != ESysProxyType.Unchanged)
        {
            type = ESysProxyType.ForcedClear;
        }

        try
        {
            var port = ResolveActiveProxyPort();
            var exceptions = config.SystemProxyItem.SystemProxyExceptions.Replace(" ", "");
            if (port <= 0)
            {
                return false;
            }
            switch (type)
            {
                case ESysProxyType.ForcedChange when Utils.IsWindows():
                    {
                        GetWindowsProxyString(config, port, out var strProxy, out var strExceptions);
                        ProxySettingWindows.SetProxy(strProxy, strExceptions, 2);
                        break;
                    }
                case ESysProxyType.ForcedChange when Utils.IsLinux():
                    await ProxySettingLinux.SetProxy(Global.Loopback, port, exceptions);
                    break;

                case ESysProxyType.ForcedChange when Utils.IsMacOS():
                    await ProxySettingOSX.SetProxy(Global.Loopback, port, exceptions);
                    break;

                case ESysProxyType.ForcedClear when Utils.IsWindows():
                    ProxySettingWindows.UnsetProxy();
                    break;

                case ESysProxyType.ForcedClear when Utils.IsLinux():
                    await ProxySettingLinux.UnsetProxy();
                    break;

                case ESysProxyType.ForcedClear when Utils.IsMacOS():
                    await ProxySettingOSX.UnsetProxy();
                    break;

                case ESysProxyType.Pac when Utils.IsWindows():
                    await SetWindowsProxyPac(port);
                    break;
            }

            if (type != ESysProxyType.Pac && Utils.IsWindows())
            {
                PacManager.Instance.Stop();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return true;
    }

    private static int ResolveActiveProxyPort()
    {
        // Keep compatibility with previous client behavior where local proxy commonly used 10808.
        if (IsLocalPortListening(10808))
        {
            return 10808;
        }

        var candidates = new[]
        {
            AppManager.Instance.GetLocalPort(EInboundProtocol.mixed),
            AppManager.Instance.GetLocalPort(EInboundProtocol.socks),
            AppManager.Instance.GetLocalPort(EInboundProtocol.socks2),
            AppManager.Instance.GetLocalPort(EInboundProtocol.socks3)
        }
        .Where(p => p > 0)
        .Distinct()
        .ToList();

        if (candidates.Count == 0)
        {
            return 0;
        }

        foreach (var port in candidates)
        {
            if (IsLocalPortListening(port))
            {
                return port;
            }
        }

        // Fallback to first configured candidate if runtime check cannot confirm listener yet.
        return candidates[0];
    }

    private static bool IsLocalPortListening(int port)
    {
        try
        {
            var listeners = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return listeners.Any(ep => ep.Port == port && (ep.Address.ToString() == "127.0.0.1" || ep.Address.Equals(System.Net.IPAddress.Loopback)));
        }
        catch
        {
            return false;
        }
    }

    private static void GetWindowsProxyString(Config config, int port, out string strProxy, out string strExceptions)
    {
        strExceptions = config.SystemProxyItem.SystemProxyExceptions.Replace(" ", "");
        if (config.SystemProxyItem.NotProxyLocalAddress)
        {
            strExceptions = $"<local>;{strExceptions}";
        }

        strProxy = string.Empty;
        if (config.SystemProxyItem.SystemProxyAdvancedProtocol.IsNullOrEmpty())
        {
            strProxy = $"{Global.Loopback}:{port}";
        }
        else
        {
            strProxy = config.SystemProxyItem.SystemProxyAdvancedProtocol
                .Replace("{ip}", Global.Loopback)
                .Replace("{http_port}", port.ToString())
                .Replace("{socks_port}", port.ToString());
        }
    }

    private static async Task SetWindowsProxyPac(int port)
    {
        var portPac = AppManager.Instance.GetLocalPort(EInboundProtocol.pac);
        await PacManager.Instance.StartAsync(port, portPac);
        var strProxy = $"{Global.HttpProtocol}{Global.Loopback}:{portPac}/pac?t={DateTime.Now.Ticks}";
        ProxySettingWindows.SetProxy(strProxy, "", 4);
    }
}
