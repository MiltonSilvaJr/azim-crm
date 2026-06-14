namespace Digest.Application.Models;

/// <summary>
/// Preferência de digest do usuário retornada por <see cref="Ports.IUserDigestPreferencePort"/>.
/// A posse desse dado é do organization (DD-003); o digest apenas lê.
/// </summary>
/// <param name="UserId">Identificador do usuário.</param>
/// <param name="OptOut">Indica se o usuário optou por não receber o digest de pendências (Req 10.1).</param>
public sealed record DigestPreference(Guid UserId, bool OptOut);
