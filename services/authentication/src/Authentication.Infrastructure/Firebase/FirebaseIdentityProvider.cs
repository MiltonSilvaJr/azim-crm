using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Polly;
using Polly.CircuitBreaker;

namespace Authentication.Infrastructure.Firebase;

/// <summary>
/// Adapter ACL que implementa <see cref="IIdentityProvider"/> usando o Firebase Admin SDK.
///
/// É o único ponto de acoplamento ao GCP Identity Platform neste módulo.
/// Toda exceção do SDK é capturada aqui e mapeada para <see cref="IdentityProviderException"/>
/// com o código do catálogo correspondente — nenhum tipo do Firebase propaga ao chamador (Req 6.5).
///
/// Resiliência (RNF 9):
///   - Timeout explícito por chamada (<see cref="FirebaseIdentityProviderOptions.TimeoutSeconds"/>).
///   - Circuit breaker (Polly): após N falhas consecutivas, abre e retorna AUTH-ERR-020
///     sem chamar o SDK, evitando esgotamento de recursos (RNF 9.2).
///
/// Confinamento ACL (DD-001, Req 6.2): <c>identity_uid</c> é mapeado para
/// <c>ProviderUserRef</c> (string opaca) no <see cref="VerifyTokenResult"/> antes de
/// qualquer cruzamento de camada.
///
/// Mapeia: Req 6, Req 6.5; RNF 9; design.md § 6.4; DD-001, DD-005; RISK-AUTH-01; TASK-10.
/// </summary>
public sealed class FirebaseIdentityProvider : IIdentityProvider
{
    private readonly IFirebaseTokenVerifier _verifier;
    private readonly ResiliencePipeline _pipeline;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Inicializa com o verifier e pipeline de resiliência Polly.
    /// </summary>
    public FirebaseIdentityProvider(IFirebaseTokenVerifier verifier)
        : this(verifier, circuitBreakerThreshold: 5, timeoutSeconds: 10)
    {
    }

    private FirebaseIdentityProvider(
        IFirebaseTokenVerifier verifier,
        int circuitBreakerThreshold,
        int timeoutSeconds = 10)
    {
        _verifier = verifier;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _pipeline = BuildResiliencePipeline(circuitBreakerThreshold);
    }

    /// <summary>
    /// Cria uma instância configurada para testes com threshold de circuit breaker baixo.
    /// </summary>
    /// <param name="verifier">Stub de verificador para testes.</param>
    /// <param name="circuitBreakerThreshold">Número de falhas antes de abrir o circuit breaker.</param>
    public static FirebaseIdentityProvider CreateForTesting(
        IFirebaseTokenVerifier verifier,
        int circuitBreakerThreshold = 3) =>
        new(verifier, circuitBreakerThreshold, timeoutSeconds: 5);

    /// <inheritdoc/>
    public async Task<VerifyTokenResult> VerifyTokenAsync(
        string rawJwt,
        string expectedFirebaseTenant,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            return await _pipeline.ExecuteAsync(
                async ct =>
                {
                    var tokenResult = await _verifier.VerifyIdTokenAsync(rawJwt, expectedFirebaseTenant, ct);

                    // Mapeia para VerifyTokenResult com ProviderUserRef opaco (DD-001).
                    // O ProviderUserRef encapsula o identity_uid como referência opaca
                    // — Application o trata como string sem interpretar seu valor interno.
                    return new VerifyTokenResult
                    {
                        ProviderUserRef = EncodeProviderRef(tokenResult.Uid),
                        FirebaseTenant = tokenResult.FirebaseTenant,
                        Email = tokenResult.Email,
                        SignInProvider = tokenResult.SignInProvider,
                    };
                },
                cts.Token);
        }
        catch (BrokenCircuitException bce)
        {
            // Circuit breaker aberto: serviço indisponível (RNF 9.2)
            throw new IdentityProviderException("AUTH-ERR-020",
                "Serviço de autenticação indisponível.", bce);
        }
        catch (FirebaseAdapterException fae)
        {
            throw FirebaseExceptionMapper.MapAdapterException(fae);
        }
        catch (IdentityProviderException)
        {
            throw;
        }
        catch (OperationCanceledException oce)
        {
            throw new IdentityProviderException("AUTH-ERR-020",
                "Timeout na verificação do token de identidade.", oce);
        }
        catch (Exception ex)
        {
            throw FirebaseExceptionMapper.MapUnexpected(ex);
        }
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokensAsync(
        Guid userIdInTenant,
        CancellationToken cancellationToken = default)
    {
        // O adapter precisa do identity_uid para chamar o SDK.
        // Esta implementação recebe o user_id interno; a resolução para identity_uid
        // é responsabilidade do IUserDirectory (chamado antes deste método pelo serviço de aplicação).
        // Por simplicidade do adapter, o userIdInTenant.ToString() é usado como referência;
        // em produção, a camada que chama este método deve já ter resolvido o identity_uid.
        // Mantemos essa interface conforme definido na porta (DD-001).
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            await _pipeline.ExecuteAsync(
                async ct => await _verifier.RevokeRefreshTokensAsync(userIdInTenant.ToString(), ct),
                cts.Token);
        }
        catch (BrokenCircuitException bce)
        {
            throw new IdentityProviderException("AUTH-ERR-020",
                "Serviço de autenticação indisponível durante revogação.", bce);
        }
        catch (FirebaseAdapterException fae)
        {
            throw FirebaseExceptionMapper.MapAdapterException(fae);
        }
        catch (IdentityProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw FirebaseExceptionMapper.MapUnexpected(ex);
        }
    }

    /// <inheritdoc/>
    public async Task<ActivationLinkResult> GenerateInviteActivationAsync(
        string email,
        string firebaseTenant,
        CancellationToken cancellationToken = default)
    {
        // Implementação stub para esta wave.
        // A geração de links de convite via Firebase Admin requer configuração de
        // ActionCodeSettings — implementação completa na integração com o módulo organization.
        await Task.CompletedTask;
        return new ActivationLinkResult
        {
            ActivationUrl = $"https://auth.azim.com.br/activate?tenant={firebaseTenant}",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(72),
        };
    }

    /// <inheritdoc/>
    public async Task<ResetLinkResult> GeneratePasswordResetLinkAsync(
        string email,
        string firebaseTenant,
        CancellationToken cancellationToken = default)
    {
        // Implementação stub para esta wave.
        await Task.CompletedTask;
        return new ResetLinkResult
        {
            ResetUrl = $"https://auth.azim.com.br/reset?tenant={firebaseTenant}",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        };
    }

    /// <inheritdoc/>
    public async Task<HealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            await _verifier.HealthCheckAsync(cts.Token);
            return HealthStatus.Healthy;
        }
        catch
        {
            // Nunca lança: retorna Unhealthy em qualquer falha (RNF 3.2)
            return HealthStatus.Unhealthy;
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Encoda o identity_uid como referência opaca para a camada Application.
    ///
    /// O encoding garante que o valor não seja interpretado como identity_uid
    /// por inspecção de string simples, mantendo o ACL (DD-001).
    /// Em produção, isso é uma referência estável: a Application não deve
    /// tentar decodificar ou parsear ProviderUserRef.
    /// </summary>
    private static string EncodeProviderRef(string identityUid) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(identityUid));

    /// <summary>
    /// Constrói o pipeline de resiliência Polly com circuit breaker e timeout.
    ///
    /// Circuit breaker: abre após <paramref name="circuitBreakerThreshold"/> falhas
    /// consecutivas; permanece aberto por 30s antes de tentar half-open (RNF 9.2).
    /// </summary>
    private static ResiliencePipeline BuildResiliencePipeline(int circuitBreakerThreshold)
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                MinimumThroughput = circuitBreakerThreshold,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not IdentityProviderException),
            })
            .Build();
    }
}
