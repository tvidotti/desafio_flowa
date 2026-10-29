using System.Globalization;
using QuickFix;

namespace FixOrderChallenge.Common;

public static class FixSettingsFactory
{
    public static SessionSettings CreateAcceptor(FixSessionOptions options) =>
        Create(options, "acceptor", $"SocketAcceptPort={options.Port}");

    public static SessionSettings CreateInitiator(FixSessionOptions options) =>
        Create(options, "initiator",
            $"SocketConnectHost={options.Host}",
            $"SocketConnectPort={options.Port}",
            "ReconnectInterval=2");

    private static SessionSettings Create(FixSessionOptions options, string connectionType, params string[] extra)
    {
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "spec", "FIX44.xml");

        var lines = new List<string>
        {
            "[DEFAULT]",
            $"ConnectionType={connectionType}",
            "StartTime=00:00:00",
            "EndTime=00:00:00",
            $"HeartBtInt={options.HeartBtInt.ToString(CultureInfo.InvariantCulture)}",
            "ResetOnLogon=Y",
            "UseDataDictionary=Y",
            $"DataDictionary={dictionaryPath}",
            $"FileLogPath={options.LogPath}",
            "",
            "[SESSION]",
            "BeginString=FIX.4.4",
            $"SenderCompID={options.SenderCompId}",
            $"TargetCompID={options.TargetCompId}",
        };
        lines.AddRange(extra);

        return new SessionSettings(new StringReader(string.Join(Environment.NewLine, lines)));
    }
}
