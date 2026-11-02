using System.ComponentModel.DataAnnotations;
using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Dtos;

/// <summary>The body of PUT /api/admin/claims/{id}/status: move a claim to its next workflow step.</summary>
// The validation attribute sits on the constructor parameter (not "[property: ...]"), because MVC reads the rules
// of a positional record from its parameters.
/// <param name="Status">The status to move to. The workflow decides whether the move is allowed.</param>
/// <param name="Note">An optional note for the history, such as what information is needed. Blank is stored as no note.</param>
public record UpdateClaimStatusRequest(
    ClaimStatus Status,
    [StringLength(ClaimStatusHistory.MaxNoteLength)] string? Note);
