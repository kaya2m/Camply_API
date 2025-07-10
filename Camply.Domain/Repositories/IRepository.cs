using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain.Repositories
{
    public interface IRepository<TEntity> where TEntity : class
    {
        // Read operations
        Task<TEntity> GetByIdAsync(Guid id);
        Task<IEnumerable<TEntity>> GetAllAsync();
        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);
        Task<TEntity> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);
        Task<TEntity> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);
        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate);
        Task<IEnumerable<TEntity>> FindByIdsAsync<TKey>(IEnumerable<TKey> ids, Expression<Func<TEntity, TKey>> idSelector);

        // Write operations
        Task<TEntity> AddAsync(TEntity entity);
        Task AddRangeAsync(IEnumerable<TEntity> entities);
        void Update(TEntity entity);
        void Remove(TEntity entity);
        void RemoveRange(IEnumerable<TEntity> entities);

        // Transaction support
        Task<int> SaveChangesAsync();
    }
}
