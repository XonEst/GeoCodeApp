using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using AddressToCoordinatesLambda.Infrastructure;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AddressToCoordinatesLambda
{
    public class Function
    {
        private static readonly AmazonDynamoDBClient dynamoClient = new AmazonDynamoDBClient();

        private static readonly GeocodeCacheRepository cacheRepository =
            new GeocodeCacheRepository(dynamoClient, "GeocodingCache");

        private static readonly GoogleGeocodeClient geocodeClient =
        new GoogleGeocodeClient(
        Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")
        ?? throw new Exception("GOOGLE_MAPS_API_KEY is not set")
    );

        public async Task<APIGatewayProxyResponse> FunctionHandler(
            APIGatewayProxyRequest request,
            ILambdaContext context)
        {
            var address =
                request.QueryStringParameters != null &&
                request.QueryStringParameters.TryGetValue("address", out var value)
                    ? value
                    : null;

            if (string.IsNullOrWhiteSpace(address))
            {
                return Response(400, "Missing 'address' query parameter");
            }

            //Cache
            var cached = await cacheRepository.GetAsync(address);
            if (cached != null)
            {
                var cachedCoordinates = JsonSerializer.Deserialize<CoordinatesDto>(cached)
                    ?? throw new Exception("Cached coordinates are null");

                var resultFromCache = new
                {
                    lat = cachedCoordinates.Lat,
                    lng = cachedCoordinates.Lng,
                    source = "cache"
                };

                return JsonResponse(200, JsonSerializer.Serialize(resultFromCache));
            }

            // Google API
            var coordinates = await geocodeClient.GetCoordinatesAsync(address);

            var jsonResult = JsonSerializer.Serialize(coordinates);

            // Save cache (30 days)
            await cacheRepository.SaveAsync(address, jsonResult, ttlDays: 30);

            // Return
            var result = new
            {
                lat = coordinates.Lat,
                lng = coordinates.Lng,
                source = "google"
            };

            return JsonResponse(200, JsonSerializer.Serialize(result));
        }

        /* ===== Helpers ===== */

        private static APIGatewayProxyResponse JsonResponse(int status, string json) =>
            new APIGatewayProxyResponse
            {
                StatusCode = status,
                Body = json,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            };

        private static APIGatewayProxyResponse Response(int status, string message) =>
            new APIGatewayProxyResponse
            {
                StatusCode = status,
                Body = message
            };
    }
}
