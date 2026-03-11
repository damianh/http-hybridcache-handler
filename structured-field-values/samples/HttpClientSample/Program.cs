// HttpClient Sample demonstrating HTTP Structured Field Values usage

using DamianH.Http.StructuredFieldValues;
using HttpClientSample;

Console.WriteLine("HTTP Structured Field Values - HttpClient Sample");
Console.WriteLine("=".PadRight(50, '='));

using var httpClient = new HttpClient();

// Example 1: Set typed headers on a request
Console.WriteLine("\n1. Setting typed headers on a request:");

var request = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/headers");

// Set a Priority header using the typed mapper
var priority = new PriorityHeader { Urgency = 1, Incremental = true };
request.SetHeader("Priority", PriorityHeader.Mapper, priority);

// Set a Cache-Control header using the typed mapper
var cacheControl = new CacheControlHeader { MaxAge = 3600, MustRevalidate = true };
request.SetHeader("Cache-Control", CacheControlHeader.Mapper, cacheControl);

Console.WriteLine($"  Priority: {request.Headers.GetValues("Priority").First()}");
Console.WriteLine($"  Cache-Control: {request.Headers.GetValues("Cache-Control").First()}");

// Example 2: Parse typed headers from strings
Console.WriteLine("\n2. Parsing typed headers:");

var parsedCache = CacheControlHeader.Mapper.Parse("max-age=3600, must-revalidate");
Console.WriteLine($"  Parsed Cache-Control:");
Console.WriteLine($"    MaxAge: {parsedCache.MaxAge}");
Console.WriteLine($"    MustRevalidate: {parsedCache.MustRevalidate}");

// Example 3: Round-trip with TryParse
Console.WriteLine("\n3. Round-trip demonstration:");

var original = new PriorityHeader { Urgency = 3, Incremental = true };
var serialized = PriorityHeader.Mapper.Serialize(original);
Console.WriteLine($"  Serialized: {serialized}");

if (PriorityHeader.Mapper.TryParse(serialized, out var parsed))
{
    Console.WriteLine($"  Parsed back - Urgency: {parsed.Urgency}, Incremental: {parsed.Incremental}");
}

// Example 4: Parse from response headers
Console.WriteLine("\n4. Using TryGetHeader pattern:");
var mockResponse = new HttpResponseMessage();
mockResponse.Headers.TryAddWithoutValidation("Cache-Control", "max-age=300, private");

if (mockResponse.TryGetHeader("Cache-Control", CacheControlHeader.Mapper, out var responseCacheControl))
{
    Console.WriteLine($"  Response Cache-Control:");
    Console.WriteLine($"    MaxAge: {responseCacheControl.MaxAge}");
    Console.WriteLine($"    Private: {responseCacheControl.Private}");
}

Console.WriteLine("\nSample completed successfully!");
