using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessClub.API.Data;

namespace FitnessClub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VisitsController : ControllerBase
{
    private readonly AppDbContext _db;

    public VisitsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAll([FromQuery] int? clientId)
    {
        var query = _db.Visits
            .Include(v => v.Client)
                .ThenInclude(c => c.User)
            .AsQueryable();

        // ✅ ФІЛЬТР ПО CLIENT
        if (clientId.HasValue)
            query = query.Where(v => v.ClientId == clientId.Value);

        var visits = await query
            .OrderByDescending(v => v.CheckIn)
            .Select(v => new
            {
                v.Id,
                v.ClientId,
                ClientName = v.Client.User.FirstName + " " + v.Client.User.LastName,
                v.CheckIn,
                v.CheckOut,
                v.Notes
            })
            .ToListAsync();

        return Ok(visits);
    }
}