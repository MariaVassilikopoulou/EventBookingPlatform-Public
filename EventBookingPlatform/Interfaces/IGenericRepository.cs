using System.Linq.Expressions;

namespace EventBookingPlatform.Interfaces
{
    public interface IGenericRepository<T> where T : class, ICosmosEntity
    {
        Task<T?> GetByIdAsync(string id, string partitionKey);
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task<T> UpdateAsync(T entity, string partitionKey);
        Task<bool> DeleteAsync(string id, string partitionKey);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string? partitionKey = null);

        Task<T> UpsertAsync(T entity);
     
    }
}
