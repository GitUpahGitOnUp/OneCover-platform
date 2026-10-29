using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCover.Disbursements.Api.Dtos;
using OneCover.Disbursements.Api.Security;
using OneCover.Disbursements.Api.Services;

namespace OneCover.Disbursements.Api.Controllers;

/// <summary>Member endpoints: a signed-in member sees only their own disbursements.</summary>
// [ApiController] turns on API conveniences, such as automatic 400 responses for bad request bodies.
[ApiController]
// The URL prefix for every action in this class: /api/disbursements
[Route("api/disbursements")]
// Only requests with a valid JWT whose role is Member may use this controller.
[Authorize(Roles = "Member")]
public class DisbursementsController : ControllerBase
{
    private readonly IDisbursementService _service;

    // DI supplies the service when ASP.NET Core creates the controller for each request.
    public DisbursementsController(IDisbursementService service)
    {
        _service = service;
    }

    /// <summary>Lists the signed-in member's disbursements, newest due date first.</summary>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Rows per page (maximum 100).</param>
    // [HttpGet] = answers GET /api/disbursements. page/pageSize come from the query string:
    // /api/disbursements?page=2&pageSize=20
    [HttpGet]
    public async Task<ActionResult<List<DisbursementDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        // User = the signed-in user from the JWT. The member id NEVER comes from the URL or body.
        var memberId = User.GetMemberId();

        // Ok(...) = HTTP 200 with the data as JSON.
        return Ok(await _service.ListForMemberAsync(memberId, page, pageSize));
    }

    /// <summary>Gets one of the signed-in member's disbursements. Returns 404 if it isn't theirs.</summary>
    /// <param name="id">The disbursement's id.</param>
    // {id} in the route is filled in from the URL: GET /api/disbursements/7 -> id = 7
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DisbursementDto>> Get(int id)
    {
        var memberId = User.GetMemberId();

        // The service throws NotFoundException when it isn't found or isn't theirs;
        // the global exception handler (a later step) will turn that into a 404.
        return Ok(await _service.GetForMemberAsync(id, memberId));
    }
}
