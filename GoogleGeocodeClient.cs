using System.Text.Json;

namespace AddressToCoordinatesLambda.Infrastructure
{
    public class GoogleGeocodeClient
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GoogleGeocodeClient(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = new HttpClient();
        }

        public async Task<CoordinatesDto> GetCoordinatesAsync(string address)
        {
            var url =
                $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Error calling Google Geocoding API");

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            var results = doc.RootElement.GetProperty("results");

            if (results.GetArrayLength() == 0)
            {
                throw new Exception("Google Geocoding returned ZERO_RESULTS");
            }

            var location = results[0]
                .GetProperty("geometry")
                .GetProperty("location");


            return new CoordinatesDto
            {
                Lat = location.GetProperty("lat").GetDouble(),
                Lng = location.GetProperty("lng").GetDouble()
            };
        }

    }
}
