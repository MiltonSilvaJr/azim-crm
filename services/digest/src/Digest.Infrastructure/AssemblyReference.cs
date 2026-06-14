namespace Digest.Infrastructure;

/// <summary>
/// Marcador de assembly para reflexão e testes de arquitetura.
/// Permite que <c>Digest.Architecture.Tests</c> resolva o assembly via
/// <c>typeof(Digest.Infrastructure.AssemblyReference).Assembly</c> (TASK-02).
/// </summary>
public sealed class AssemblyReference;
