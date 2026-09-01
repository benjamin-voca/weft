using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Weft.Api.Common;
using Weft.Api.Data;
using Weft.Api.Domain;

namespace Weft.Api.Features;

public sealed record LoginRequest(string Email, string Password);
public sealed record UserDto(string Email, string DisplayName, string Role);

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest req, WeftDb db, HttpContext ctx) =>
        {
            var email = (req.Email ?? "").Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

            if (user is null || !PasswordHasher.Verify(req.Password ?? "", user.PasswordHash))
                return Results.Json(new { error = "Invalid email or password" }, statusCode: 401);

            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("sub", user.Id.ToString())
            ], CookieAuthenticationDefaults.AuthenticationScheme);

            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });

            return Results.Ok(new UserDto(user.Email, user.DisplayName, user.Role));
        }).AllowAnonymous();

        group.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { ok = true });
        }).AllowAnonymous();

        group.MapGet("/me", (HttpContext ctx) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            return Results.Ok(new UserDto(
                ctx.User.FindFirstValue(ClaimTypes.Email) ?? "",
                ctx.User.FindFirstValue(ClaimTypes.Name) ?? "",
                ctx.User.FindFirstValue(ClaimTypes.Role) ?? "Member"));
        }).RequireAuthorization();
    }
}
