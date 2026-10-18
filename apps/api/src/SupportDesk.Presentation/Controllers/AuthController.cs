using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(LoginCommandHandler login) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await login.HandleAsync(request, ct);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = InvalidCredentialsMessage,
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var id = User.FindFirst("sub")?.Value;
        var fullName = User.FindFirst("name")?.Value;
        var email = User.FindFirst("email")?.Value;
        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var agentId) ||
            string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized();
        }

        return Ok(new AuthenticatedAgentDto(agentId, fullName, email));
    }
}
