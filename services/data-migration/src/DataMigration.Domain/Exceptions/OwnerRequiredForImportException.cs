namespace DataMigration.Domain.Exceptions;

/// <summary>
/// Exceção de domínio lançada quando a transição para <c>importing</c> é tentada
/// mas ainda existem oportunidades sem owner atribuído.
///
/// Código de erro: <c>MIG-ERR-006</c> (design §12).
/// HTTP mapeado: 409 Conflict.
///
/// Rastreia: design §4.1, §4.5, §12 (MIG-ERR-006), PBT-07, TASK-03.
/// </summary>
public sealed class OwnerRequiredForImportException : Exception
{
    /// <summary>Código de erro canônico do catálogo (design §12).</summary>
    public string ErrorCode => "MIG-ERR-006";

    /// <summary>Número de oportunidades que ainda estão sem owner atribuído.</summary>
    public int OwnerlessCandidateCount { get; }

    /// <summary>
    /// Cria a exceção informando quantas oportunidades estão sem owner.
    /// </summary>
    public OwnerRequiredForImportException(int ownerlessCandidateCount)
        : base($"Import bloqueado: há {ownerlessCandidateCount} oportunidade(s) sem responsável. " +
               "Atribua owner a todas as oportunidades antes de executar o import. (MIG-ERR-006)")
    {
        OwnerlessCandidateCount = ownerlessCandidateCount;
    }
}
