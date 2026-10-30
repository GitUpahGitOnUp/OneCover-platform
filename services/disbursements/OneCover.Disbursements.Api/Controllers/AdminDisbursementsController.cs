using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCover.Disbursements.Api.Dtos;
using OneCover.Disbursements.Api.Models.Enums;
using OneCover.Disbursements.Api.Services;

namespace OneCover.Disbursements.Api.Controllers;

/// <summary>Admin endpoints: see every disbursement and mark them Paid or Failed.</summary>
[ApiController]
[Route("api/admin/disbursements")]
// Only the Admin role may call anything in this controller. A Member gets 403 Forbidden.
[Authorize(Roles = "Admin")]
public class AdminDisbursementsController : ControllerBase
{
    private readonly IDisbursementService _service;

    public AdminDisbursementsController(IDisbursementService service)
    {
        _service = service;
    }

    /// <summary>Lists all disbursements, optionally only those with one status.</summary>
    /// <param name="status">Optional filter: Pending, Paid or Failed. Leave out for all.</param>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Rows per page (maximum 100).</param>
    [EndpointSummary("List disbursements as an admin")]
    [EndpointDescription("Requires an Admin JWT. Optionally filter by Pending, Paid, or Failed status.")]
    [HttpGet]
    public async Task<ActionResult<List<DisbursementDto>>> List(
        [FromQuery] DisbursementStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return Ok(await _service.ListAllAsync(status, page, pageSize));
    }

    /// <summary>Gets any disbursement by id. Returns 404 if it doesn't exist.</summary>
    /// <param name="id">The disbursement's id.</param>
    [EndpointSummary("Get any disbursement as an admin")]
    [EndpointDescription("Requires an Admin JWT.")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DisbursementDto>> Get(int id)
    {
        return Ok(await _service.GetAsync(id));
    }

    /// <summary>Marks a Pending disbursement as Paid or Failed.</summary>
    /// <param name="id">The disbursement's id.</param>
    /// <param name="request">The new status, plus a reason when failing.</param>
    [EndpointSummary("Update a disbursement's status")]
    [EndpointDescription("Requires an Admin JWT. Only Pending disbursements can be changed to Paid or Failed; Failed requires a reason.")]
    // [HttpPut("{id:int}/status")] = answers PUT /api/admin/disbursements/7/status
    // The request body (JSON) is automatically turned into an UpdateDisbursementStatusRequest object.
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<DisbursementDto>> UpdateStatus(int id, UpdateDisbursementStatusRequest request)
    {
        return Ok(await _service.UpdateStatusAsync(id, request));
    }
}
