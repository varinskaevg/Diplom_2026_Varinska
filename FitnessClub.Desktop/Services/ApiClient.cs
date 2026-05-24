using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FitnessClub.Desktop.Services;

public static class ApiClient
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    private static readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://localhost:5176/")
    };

    public static void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<T?> GetAsync<T>(string url)
    {
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
    }

    public static async Task<T?> PostAsync<T>(string url, object body)
    {
        var response = await _http.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
    }

    public static async Task PutAsync(string url, object body)
    {
        var response = await _http.PutAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
    }

    // НОВИЙ — PutAsync з поверненням результату
    public static async Task<T?> PutAsync<T>(string url, object body)
    {
        var response = await _http.PutAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
    }

    public static async Task DeleteAsync(string url)
    {
        var response = await _http.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }
}