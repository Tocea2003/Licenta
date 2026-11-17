using Confluent.Kafka;

namespace TursibMessaging;

/// <summary>
/// Conexiunea la Kafka, comună serviciului de ingestie și backend-ului. Local merge fără securitate;
/// brokerele gestionate (Redpanda Cloud, Upstash, Confluent Cloud) cer <c>SaslSsl</c> cu utilizator și parolă.
/// </summary>
public class KafkaConnectionOptions
{
    public string? BootstrapServers { get; set; }
    /// <summary>Plaintext (implicit), Ssl, SaslPlaintext sau SaslSsl.</summary>
    public SecurityProtocol? SecurityProtocol { get; set; }
    /// <summary>Plain, ScramSha256 sau ScramSha512 (ce cere furnizorul).</summary>
    public SaslMechanism? SaslMechanism { get; set; }
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
    /// <summary>Calea unui certificat CA (PEM) pentru un broker cu certificat propriu; implicit se folosește trust store-ul sistemului.</summary>
    public string? SslCaLocation { get; set; }

    public void Apply(ClientConfig config)
    {
        config.BootstrapServers = BootstrapServers;
        if (SecurityProtocol != null) config.SecurityProtocol = SecurityProtocol;
        if (SaslMechanism != null) config.SaslMechanism = SaslMechanism;
        if (!string.IsNullOrEmpty(SaslUsername)) config.SaslUsername = SaslUsername;
        if (!string.IsNullOrEmpty(SaslPassword)) config.SaslPassword = SaslPassword;
        if (!string.IsNullOrEmpty(SslCaLocation)) config.SslCaLocation = SslCaLocation;
    }
}
