using System.Net.Http.Json;
using System.Text.Json;
namespace LimosDispatch.Core.Services;

public sealed record AddressSuggestion(string PlaceId, string Description)
{
    public string PrimaryText => Description.Split(',', 2)[0].Trim();
    public string SecondaryText => Description.Contains(',') ? Description.Split(',', 2)[1].Trim() : "";
}
public sealed record GoogleAddress(string PlaceId, string Address);
public sealed record RoadRoute(double Meters)
{
    public double Miles => Meters / 1609.344;
}
public sealed class GoogleAddresses(HttpClient client, string apiKey)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private async Task<JsonElement> Request(string url, object? body, string fields, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, url);
        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask", fields);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.Forbidden
                ? "Google address services are unavailable. Check Places API (New), Routes API, billing and key restrictions."
                : "Google could not complete this request. Check the addresses and try again.");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
    }
    public async Task<List<AddressSuggestion>> SearchAsync(string input, string session, CancellationToken ct = default)
    {
        if (input.Trim().Length < 3) return [];
        var json = await Request("https://places.googleapis.com/v1/places:autocomplete", new { input = input.Trim(), sessionToken = session, languageCode = "en" }, "suggestions.placePrediction.placeId,suggestions.placePrediction.text", ct);
        if (!json.TryGetProperty("suggestions", out var suggestions)) return [];
        return suggestions.EnumerateArray().Where(s => s.TryGetProperty("placePrediction", out _)).Select(s => s.GetProperty("placePrediction"))
            .Select(p => new AddressSuggestion(p.GetProperty("placeId").GetString()!, p.GetProperty("text").GetProperty("text").GetString()!)).ToList();
    }
    public async Task<GoogleAddress> SelectAsync(AddressSuggestion suggestion, string session, CancellationToken ct = default)
    {
        var json = await Request($"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(suggestion.PlaceId)}?sessionToken={Uri.EscapeDataString(session)}&languageCode=en", null, "id,formattedAddress", ct);
        return new GoogleAddress(json.GetProperty("id").GetString()!, json.GetProperty("formattedAddress").GetString()!);
    }
    public async Task<(double Latitude,double Longitude)> LocateAsync(string address,CancellationToken ct=default)
    {
        var json=await Request("https://places.googleapis.com/v1/places:searchText",new{textQuery=address,languageCode="en",maxResultCount=1},"places.location",ct);
        if(!json.TryGetProperty("places",out var places)||places.GetArrayLength()==0)throw new InvalidOperationException("No map location was found for this address.");
        var point=places[0].GetProperty("location");return(point.GetProperty("latitude").GetDouble(),point.GetProperty("longitude").GetDouble());
    }
    public async Task<RoadRoute> RouteAsync(string from, string to, CancellationToken ct = default)
    {
        var json = await Request("https://routes.googleapis.com/directions/v2:computeRoutes", new { origin = new { address = from }, destination = new { address = to }, travelMode = "DRIVE", computeAlternativeRoutes = false, languageCode = "en-US" }, "routes.distanceMeters,geocodingResults.origin.partialMatch,geocodingResults.destination.partialMatch", ct);
        if (json.TryGetProperty("geocodingResults", out var geocoding))
            foreach (var end in new[] { "origin", "destination" })
                if (geocoding.TryGetProperty(end, out var place) && place.TryGetProperty("partialMatch", out var partial) && partial.ValueKind == JsonValueKind.True)
                    throw new InvalidOperationException("Google only partially matched an address. Please select a more precise address.");
        if (!json.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0 || !routes[0].TryGetProperty("distanceMeters", out var distance) || !distance.TryGetDouble(out var meters) || !double.IsFinite(meters) || meters <= 0)
            throw new InvalidOperationException("No driving route was found between these addresses.");
        return new RoadRoute(meters);
    }
}
