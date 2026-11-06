using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SafeCore.Shared.Contracts;
using SafeCore.Shared.Kernel;

namespace SafeCore.Shared.Data;

public sealed class EfRepository<TEntity>(SafeCoreDbContext context) : IRepository<TEntity> where TEntity : Entity
{
    private readonly DbSet<TEntity> _set = context.Set<TEntity>();

    public async Task<IReadOnlyList<TEntity>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 250);
        var offset = (int)Math.Min((long)(safePage - 1) * safePageSize, int.MaxValue);
        return await _set.AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .Skip(offset)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> ListWhereAsync(Expression<Func<TEntity, bool>> predicate, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 250);
        var offset = (int)Math.Min((long)(safePage - 1) * safePageSize, int.MaxValue);
        return await _set.Where(predicate).OrderByDescending(item => item.CreatedAt)
            .Skip(offset)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _set.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        _set.AnyAsync(predicate, cancellationToken);

    public void Add(TEntity entity) => _set.Add(entity);
    public void Remove(TEntity entity) => _set.Remove(entity);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);
}
