namespace FixOrderChallenge.Common;

/// <summary>
/// Parâmetros de uma sessão FIX 4.4. São convertidos em <see cref="QuickFix.SessionSettings"/>
/// por <see cref="FixSettingsFactory"/>, o que permite configurar host/porta via appsettings
/// ou variáveis de ambiente sem manter arquivos .cfg separados.
/// </summary>
public sealed class FixSessionOptions
{
    public string SenderCompId { get; set; } = "";
    public string TargetCompId { get; set; } = "";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5001;
    public int HeartBtInt { get; set; } = 30;
    public string LogPath { get; set; } = "logs/fix";
}
