namespace DataMigration.Application.Exceptions;

/// <summary>
/// Exceção base para erros de domínio da camada Application do módulo data-migration.
///
/// Transporta o código de erro do catálogo (design §12) para mapeamento
/// HTTP no controller (Onda 5).
///
/// Rastreia: design §12, TASK-08.
/// </summary>
public class MigrationDomainException : Exception
{
    /// <summary>Código de erro do catálogo (ex: "MIG-ERR-001").</summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Cria uma <see cref="MigrationDomainException"/> com código e mensagem.
    /// </summary>
    public MigrationDomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Cria uma <see cref="MigrationDomainException"/> com código, mensagem e causa.
    /// </summary>
    public MigrationDomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
