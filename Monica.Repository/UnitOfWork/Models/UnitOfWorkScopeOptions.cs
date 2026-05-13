using System.Data;

namespace Monica.Repository.UnitOfWork.Models;

/// <summary>
/// Describes how a unit-of-work scope should be opened.
/// </summary>
/// <param name="IsTransactional">
/// Whether the scope should start database transactions for participating DbContexts. The default is transactional because
/// most write workflows expect commit and rollback to be coordinated by the unit of work.
/// </param>
/// <param name="IsolationLevel">
/// Optional database isolation level. Leave <see langword="null"/> to use the provider default.
/// </param>
/// <param name="RequiresNew">
/// Whether a new outer unit of work should be created even when one is already active.
/// </param>
/// <param name="Timeout">
/// Optional command timeout in milliseconds applied to participating relational DbContexts that have no explicit timeout.
/// </param>
public sealed record UnitOfWorkScopeOptions(
    bool IsTransactional = true,
    IsolationLevel? IsolationLevel = null,
    bool RequiresNew = false,
    int? Timeout = null);
