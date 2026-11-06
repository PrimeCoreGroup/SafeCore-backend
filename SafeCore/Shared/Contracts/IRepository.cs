using System.Linq.Expressions;
using SafeCore.Shared.Kernel;

namespace SafeCore.Shared.Contracts;

public interface IRepository<TEntity> where TEntity : Entity
{
    Task<IReadOnlyList<TEntity>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListWhereAsync(Expression<Func<TEntity, bool>> predicate, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    void Add(TEntity entity);
    void Remove(TEntity entity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
