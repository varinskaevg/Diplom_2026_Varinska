namespace FitnessClub.Desktop.Models;

public class ClientItem
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public object? ActiveMembership { get; set; }
    public DateTime RegistrationDate { get; set; } = DateTime.Now;

    public string FullName => $"{FirstName} {LastName}";
    public bool HasActiveMembership => ActiveMembership != null;
    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpper();
}