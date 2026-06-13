namespace NotificationDelivery.Application.Ports;

/// <summary>
/// Resposta bruta do provedor de e-mail, antes da tradução pela ACL.
///
/// Tipo de transporte interno entre a Infrastructure e a Application.
/// Nenhum tipo de SDK de provedor aparece nesta estrutura — a tradução
/// para <see cref="NotificationDelivery.Contracts.SendResult"/> é feita
/// pelo <c>ProviderResponseMapper</c> na Infrastructure (Req 1.2, RNF 1, DD-003).
/// </summary>
/// <param name="IsSuccess">Indica se o provedor aceitou e enfileirou a mensagem.</param>
/// <param name="MessageId">Identificador de mensagem atribuído pelo provedor, presente quando <paramref name="IsSuccess"/> é <c>true</c>.</param>
/// <param name="ErrorCode">Código canônico de erro do catálogo <c>FailureCode</c>, presente quando não é sucesso.</param>
/// <param name="ErrorMessage">Mensagem de erro legível, sem PII (RNF 4).</param>
/// <param name="IsRetriable">Indica se a falha é transiente e pode ser retentada.</param>
public sealed record ProviderResponse(
    bool IsSuccess,
    string? MessageId,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsRetriable);

/// <summary>
/// Porta de saída para o provedor de e-mail transacional.
///
/// Definida em <c>Application</c> e implementada em <c>Infrastructure</c>
/// por adaptadores concretos (ex.: <c>ResendEmailSender</c>), conforme DD-003.
///
/// A interface é provider-agnóstica: nenhum tipo, exceção ou símbolo de
/// provedor cruza esta fronteira (Req 1.2, RNF 1, DD-003).
/// </summary>
public interface IEmailProviderClient
{
    /// <summary>
    /// Envia a mensagem ao provedor de e-mail e retorna a resposta normalizada.
    ///
    /// <para>Não aplica resiliência — retry, timeout e circuit breaker são
    /// responsabilidade do <c>ResilientEmailSender</c> (DD-004).</para>
    ///
    /// <para>Nenhuma exceção de provedor é propagada: o adaptador captura
    /// exceções específicas e as traduz para <see cref="ProviderResponse"/>
    /// com <c>IsSuccess=false</c> e código canônico.</para>
    /// </summary>
    /// <param name="message">Mensagem de e-mail válida e imutável.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resposta normalizada do provedor.</returns>
    Task<ProviderResponse> SendAsync(
        NotificationDelivery.Contracts.EmailMessage message,
        CancellationToken cancellationToken = default);
}
