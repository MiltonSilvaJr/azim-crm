namespace Digest.Application;

/// <summary>
/// Marcador de assembly para reflexão e testes de arquitetura.
/// Permite que <c>Digest.Architecture.Tests</c> resolva o assembly via
/// <c>typeof(Digest.Application.AssemblyReference).Assembly</c> (TASK-02).
/// </summary>
public sealed class AssemblyReference;
