namespace FitnessClub.Desktop;

public static class AppSession
{
    public static string Token { get; set; } = "";
    public static string Role { get; set; } = "";
    public static string FullName { get; set; } = "";
    public static string Email { get; set; } = "";
    public static string FirstName => FullName.Split(' ').FirstOrDefault() ?? FullName;
    public static bool IsAdmin => Role == "Admin";
    public static bool IsManager => Role == "Manager" || Role == "Admin";
    public static bool IsTrainer => Role == "Trainer";
    public static void Clear()
    {
        Token = "";
        Role = "";
        FullName = "";
        Email = "";
    }
}