using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Dtos;

/// <summary>One status change in a claim's timeline, as the API returns it.</summary>
/// <param name="FromStatus">The status before the change. Null on the first row, because the claim did not exist yet.</param>
/// <param name="ToStatus">The status after the change.</param>
/// <param name="ChangedByUserId">The user who made the change: the member when filing, an admin for later moves.</param>
/// <param name="ChangedAt">When the change happened.</param>
/// <param name="Note">An optional admin note, such as what information was requested. Null when there is none.</param>
public record ClaimHistoryDto(
    ClaimStatus? FromStatus,
    ClaimStatus ToStatus,
    int ChangedByUserId,
    DateTimeOffset ChangedAt,
    string? Note);
