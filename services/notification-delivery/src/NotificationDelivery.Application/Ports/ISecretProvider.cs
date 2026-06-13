namespace NotificationDelivery.Application.Ports;

/// <summary>
/// Porta de saída para recuperação de segredos do provedor de e-mail.
///
/// Definida em <c>Application</c> e implementada em <c>Infrastructure</c>
/// por <c>SecretManagerProvider</c> (DD-007, Req 10, RNF 6).
///
/// <para>Invariantes de segurança (RNF 6, DD-007):</para>
/// <list type="bullet">
///   <item><description>O valor do segredo nunca é logado em nenhuma condição (RNF-6.3).</description></item>
///   <item><description>Falha de acesso ao Secret Manager lança <see cref="SecretProviderException"/>, que o sender deve converter em <c>SendResult(PermanentFailure, NOTIF-ERR-040)</c>.</description></item>
///   <item><description>O segredo nunca é retornado em <c>ToString()</c> nem em mensagens de exceção.</description></item>
/// </list>
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Recupera o valor de um segredo pelo nome lógico.
    ///
    /// <para>Implementações devem usar cache com TTL curto para evitar uma chamada
    /// ao Secret Manager por envio (DD-007, design §6.2).</para>
    ///
    /// <para>A credencial nunca deve aparecer em nenhum log, trace ou mensagem de erro.</para>
    /// </summary>
    /// <param name="secretName">
    /// Nome lógico do segredo (ex.: <c>"RESEND_API_KEY"</c>, <c>"SENDGRID_API_KEY"</c>).
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Valor do segredo como string.</returns>
    /// <exception cref="SecretProviderException">
    /// Lançada quando o acesso ao Secret Manager falha (ausência, permissão negada, timeout).
    /// O sender converte em <c>SendResult(PermanentFailure, NOTIF-ERR-040)</c>.
    /// </exception>
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exceção lançada quando o <see cref="ISecretProvider"/> falha ao recuperar um segredo.
///
/// <para>O sender a converte em <c>SendResult(PermanentFailure, NOTIF-ERR-040)</c>
/// sem propagar ao chamador (Req 3.5, design §12).</para>
///
/// <para>A mensagem desta exceção não deve conter o valor do segredo (RNF-6.3).</para>
/// </summary>
public sealed class SecretProviderException : Exception
{
    /// <summary>Nome lógico do segredo que causou a falha.</summary>
    public string SecretName { get; }

    /// <summary>
    /// Constrói a exceção com o nome do segredo e uma mensagem sem o valor da credencial.
    /// </summary>
    /// <param name="secretName">Nome lógico do segredo (sem o valor).</param>
    /// <param name="message">Mensagem descritiva sem PII nem credencial.</param>
    /// <param name="innerException">Causa raiz (optional).</param>
    public SecretProviderException(string secretName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        SecretName = secretName ?? string.Empty;
    }
}
