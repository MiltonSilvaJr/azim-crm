using Xunit;

// Desabilita paralelismo de collections para evitar race conditions com Testcontainers.
// Múltiplos DbContexts com app.current_tenant em sessões paralelas causam isolamento instável.
// Testcontainers requer que a fixture PostgresFixture inicialize completamente antes
// de qualquer teste da collection executar.
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
