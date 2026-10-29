namespace OneCover.Identity.Api.Dtos;

/// <summary>A signed bearer token and the authenticated user's role.</summary>
public sealed record AuthResponse(string Token, string Role);
