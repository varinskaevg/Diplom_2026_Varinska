using System.Net.Http;
using System.Net.Http.Json;
using FitnessClub.Desktop.Models;
using FitnessClub.Desktop.Services;

namespace FitnessClub.Desktop.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private string _email = "";
    private string _errorMessage = "";
    private bool _isLoading;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public Action? OnLoginSuccess { get; set; }

    private static readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://localhost:5176/")
    };

    public LoginViewModel()
    {
    }

    public async Task LoginAsync(string password)
    {
        if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Введіть email та пароль";
            return;
        }

        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", new
            {
                email = Email,
                password
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (result != null)
                {
                    AppSession.Token = result.Token;
                    AppSession.Role = result.Role;
                    AppSession.FullName = result.FullName;
                    AppSession.Email = Email;
                    ApiClient.SetToken(result.Token);
                    OnLoginSuccess?.Invoke();
                }
            }
            else
            {
                ErrorMessage = "Невірний email або пароль";
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Не вдалось підключитись до сервера.\nПереконайся що API запущений.";
        }
    }
}