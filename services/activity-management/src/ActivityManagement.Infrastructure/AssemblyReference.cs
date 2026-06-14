using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ActivityManagement.Infrastructure.Tests")]

namespace ActivityManagement.Infrastructure;

/// <summary>
/// Marcador de assembly para localização do Assembly de Infrastructure em tempo de execução.
/// Utilizado por <c>Architecture.Tests</c> via reflexão para validar as regras de
/// dependência entre camadas (design §3, TASK-01).
/// </summary>
public sealed class AssemblyReference;
