using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;

namespace AddressToCoordinatesLambda.Infrastructure
{
    public class GeocodeCacheRepository
    {
        private readonly ITable _table;

        public GeocodeCacheRepository(IAmazonDynamoDB dynamoDb, string tableName)
        {
            _table = new TableBuilder(dynamoDb, tableName)
                .AddHashKey("address", DynamoDBEntryType.String)
                .Build();
        }

        public async Task<string?> GetAsync(string address)
        {
            var doc = await _table.GetItemAsync(address);

            if (doc == null)
            {
                return null; // cache miss
            }

            if (!doc.TryGetValue("responseJson", out var responseEntry))
            {
                return null; // incomplete
            }

            return responseEntry.AsString();
        }

        public async Task SaveAsync(string address, string responseJson, int ttlDays)
        {
            var ttl = DateTimeOffset.UtcNow
                .AddDays(ttlDays)
                .ToUnixTimeSeconds();

            var doc = new Document
            {
                ["address"] = address,
                ["responseJson"] = responseJson,
                ["expiresAt"] = ttl
            };

            await _table.PutItemAsync(doc);
        }
    }
}
