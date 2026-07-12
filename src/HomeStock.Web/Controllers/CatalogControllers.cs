using HomeStock.Application.Abstractions;
using HomeStock.Application.Models;
using HomeStock.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeStock.Web.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll([FromQuery] bool includeArchived, CancellationToken ct)
        => Ok(await categories.GetAllAsync(includeArchived, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Get(int id, CancellationToken ct)
        => await categories.GetAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost]
    [Authorize(Policy = Roles.CanEditPolicy)]
    public async Task<ActionResult> Create([FromBody] CategoryEditModel model, CancellationToken ct)
    {
        var r = await categories.CreateAsync(model, ct);
        return r.Succeeded ? CreatedAtAction(nameof(Get), new { id = r.Value }, new { id = r.Value }) : BadRequest(new { errors = r.Errors });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Roles.CanEditPolicy)]
    public async Task<ActionResult> Update(int id, [FromBody] CategoryEditModel model, CancellationToken ct)
    {
        var r = await categories.UpdateAsync(id, model, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { errors = r.Errors });
    }
}

[ApiController]
[Route("api/locations")]
[Authorize]
public class LocationsController(ILocationService locations) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationDto>>> GetAll([FromQuery] bool includeArchived, CancellationToken ct)
        => Ok(await locations.GetAllAsync(includeArchived, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LocationDto>> Get(int id, CancellationToken ct)
        => await locations.GetAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<LocationDto>> GetByCode(string code, CancellationToken ct)
        => await locations.GetByCodeAsync(code, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost]
    [Authorize(Policy = Roles.CanEditPolicy)]
    public async Task<ActionResult> Create([FromBody] LocationEditModel model, CancellationToken ct)
    {
        var r = await locations.CreateAsync(model, ct);
        return r.Succeeded ? CreatedAtAction(nameof(Get), new { id = r.Value }, new { id = r.Value }) : BadRequest(new { errors = r.Errors });
    }
}

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) => Ok(await dashboard.GetAsync(ct));
}
