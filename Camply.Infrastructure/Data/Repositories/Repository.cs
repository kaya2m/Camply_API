using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Camply.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Camply.Infrastructure.Data.Repositories
{
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        protected readonly CamplyDbContext _context;
        protected readonly DbSet<TEntity> _dbSet;

        public Repository(CamplyDbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }

        public virtual async Task<TEntity> GetByIdAsync(Guid id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<TEntity> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.SingleOrDefaultAsync(predicate);
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }
        // Repository sınıfına ekleyin:
        public virtual async Task<IEnumerable<TEntity>> FindByIdsAsync<TKey>(IEnumerable<TKey> ids, Expression<Func<TEntity, TKey>> idSelector)
        {
            if (ids == null || !ids.Any())
                return Enumerable.Empty<TEntity>();

            var idList = ids.ToList();

            var parameter = idSelector.Parameters[0];
            var memberExpression = idSelector.Body as MemberExpression;

            if (memberExpression == null)
                throw new ArgumentException("idSelector must be a simple property selector", nameof(idSelector));

            var containsMethod = typeof(List<TKey>).GetMethod("Contains", new[] { typeof(TKey) });
            var listConstant = Expression.Constant(idList);
            var containsExpression = Expression.Call(listConstant, containsMethod, memberExpression);
            var lambda = Expression.Lambda<Func<TEntity, bool>>(containsExpression, parameter);

            return await _dbSet.Where(lambda).ToListAsync();
        }
        public virtual async Task<TEntity> AddAsync(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public virtual void Update(TEntity entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        public virtual void Remove(TEntity entity)
        {
            _dbSet.Remove(entity);
        }

        public virtual void RemoveRange(IEnumerable<TEntity> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}

