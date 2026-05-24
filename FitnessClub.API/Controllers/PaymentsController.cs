using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Data;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PaymentsController(AppDbContext db) => _db = db;

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAll([FromQuery] int? clientId)
    {
        var query = _db.Payments
            .Include(p => p.Client)
                .ThenInclude(c => c.User)
            .AsQueryable();

        // ✅ ФІЛЬТР ПО CLIENT
        if (clientId.HasValue)
            query = query.Where(p => p.ClientId == clientId.Value);

        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new
            {
                p.Id,
                ClientId = p.ClientId,
                ClientName = p.Client.User.FirstName + " " + p.Client.User.LastName,
                p.Amount,
                p.PaymentMethod,
                p.Description,
                p.PaymentDate,
                p.Status
            })
            .ToListAsync();

        return Ok(payments);
    }
}