using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EmployeeManagement.Web.Services;

internal static class HttpServiceHelper
{
    internal static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await ExtractErrorMessageAsync(response);
        var statusCode = (int)response.StatusCode;
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"Unable to {operation} (HTTP {statusCode} {response.ReasonPhrase})."
            : $"Unable to {operation}: {detail}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    internal static HttpRequestException TranslateNetworkException(Exception ex, string operation)
    {
        return ex switch
        {
            TaskCanceledException => new HttpRequestException(
                $"The request to {operation} timed out. Please verify that the API server is running.", ex),
            HttpRequestException httpEx => httpEx,
            _ => new HttpRequestException(
                $"Network error while attempting to {operation}. Please check API connectivity.", ex)
        };
    }

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return response.StatusCode == HttpStatusCode.NotFound ? "Resource not found." : string.Empty;
            }

            if (body.TrimStart().StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("errors", out var errorsElement)
                    && errorsElement.ValueKind == JsonValueKind.Object)
                {
                    var messages = new List<string>();
                    foreach (var prop in errorsElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                var msg = item.GetString();
                                if (!string.IsNullOrWhiteSpace(msg))
                                {
                                    messages.Add(msg);
                                }
                            }
                        }
                    }

                    if (messages.Count > 0)
                    {
                        return string.Join(" ", messages);
                    }
                }

                if (doc.RootElement.TryGetProperty("title", out var titleElement))
                {
                    var title = titleElement.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        return title;
                    }
                }
            }

            return body.Trim('"');
        }
        catch
        {
            return string.Empty;
        }
    }
}

