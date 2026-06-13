namespace TenantAdministration.Application.Exceptions;

/// <summary>
/// Exceção lançada pelo <c>AuthorizationBehavior</c> quando o ator não tem
/// o papel/plano exigido pela operação.
/// Mapeada para HTTP 403 Forbidden na camada de API.
/// </summary>
public sealed class AuthorizationException : Exception
{
    /// <summary>Cria uma <see cref="AuthorizationException"/> com a mensagem informada.</summary>
    public AuthorizationException(string message) : base(message) { }
}
