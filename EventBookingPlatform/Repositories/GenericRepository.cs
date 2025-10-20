using EventBookingPlatform.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using System.Net;

namespace EventBookingPlatform.Repositories
{
    public class GenericRepository<T>: IGenericRepository<T> where T : class, ICosmosEntity
    {
        private readonly Container _container;
        private readonly ILogger<GenericRepository<T>> _logger;

        public GenericRepository(CosmosClient client, IOptions<CosmosDbSettings> options, ILogger<GenericRepository<T>> logger)
        {
            _logger = logger;
            var settings = options.Value;

            var containerName = Activator.CreateInstance<T>().ContainerName;

            _container = client
                    .GetDatabase(settings.DatabaseName)
                    .GetContainer(containerName);
            Console.WriteLine($"Connecting repository for {typeof(T).Name} to container {Activator.CreateInstance<T>().ContainerName}");

        }

        public async Task<T> AddAsync(T entity)
        {
            try
            {
                var response = await _container.CreateItemAsync(
                    entity,
                    new PartitionKey(entity.PartitionKey));

                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex,
                    "Cosmos failed to create item. Status={StatusCode} Response={ErrorMessage}",
                    ex.StatusCode, ex.Message);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string id, string partitionKey)
        {
            try
            {
                var response = await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKey));

                return response.StatusCode == HttpStatusCode.NoContent;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex,
                    "Cosmos failed to delete item {Id}. Status={StatusCode} Response={ErrorMessage}",
                    id, ex.StatusCode, ex.Message);
                throw;
            }
        }
        public async Task<T> UpdateAsync(T entity, string partitionKey)
        {
            try
            {
                var response = await _container.ReplaceItemAsync(entity, entity.Id, new PartitionKey(partitionKey));

                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos failed to update item. Status={StatusCode} Response={ErrorMessage}", ex.StatusCode, ex.Message);
                throw;
            }
        }
        public async Task<T?> GetByIdAsync(string id, string partitionKey)
        {
            try
            {
                var response = await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Item not found. Id={Id}, PartitionKey={PartitionKey}", id, partitionKey);
                return null;
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            var queryable = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false);

            var feedIterator = queryable.ToFeedIterator();
            var results = new List<T>();

            while (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            return results;
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string? partitionKey = null)
        {
            QueryRequestOptions? options = null;

            if (!string.IsNullOrEmpty(partitionKey))
            {
                options = new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey)
                };
            }

            var queryable = _container.GetItemLinqQueryable<T>(
                allowSynchronousQueryExecution: false,
                requestOptions: options
            )
            .Where(predicate);

            using var feedIterator = queryable.ToFeedIterator();
            var results = new List<T>();

            while (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            return results;

        }


        public async Task<T> UpsertAsync(T entity)
        {
            try
            {
                var response = await _container.UpsertItemAsync(
                    entity,
                    new PartitionKey(entity.PartitionKey));

                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex,
                    "Cosmos failed to upsert item. Status={StatusCode} Response={ErrorMessage}",
                    ex.StatusCode, ex.Message);
                throw;
            }
        }

    }
}
