using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Quantira.Infrastructure.Persistence;

namespace Quantira.WebAPI.Controllers;

/// <summary>
/// Handles user registration, login, token refresh and logout.
/// Uses ASP.NET Core Identity for user management and issues
/// short-lived JWT access tokens with long-lived refresh tokens.
/// Refresh tokens are stored as hashed values in the Identity
/// UserTokens table — plain text tokens never touch the database.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    /// <summary>Registers a new user account.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(new
            {
                errors = result.Errors.Select(e => e.Description)
            });

        var tokens = await GenerateTokensAsync(user);

        _logger.LogInformation(
            "[Auth] New user registered: {Email}", request.Email);

        return CreatedAtAction(nameof(Register), tokens);
    }

    /// <summary>Authenticates a user and returns JWT + refresh token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { message = "Invalid email or password." });

        if (user.DeletedAt.HasValue)
            return Unauthorized(new { message = "This account has been deactivated." });

        var tokens = await GenerateTokensAsync(user);

        _logger.LogInformation("[Auth] User logged in: {Email}", request.Email);

        return Ok(tokens);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new JWT access token.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var principal = GetPrincipalFromExpiredToken(request.AccessToken);

        if (principal is null)
            return Unauthorized(new { message = "Invalid access token." });

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId ?? string.Empty);

        if (user is null)
            return Unauthorized(new { message = "User not found." });

        var storedHash = await _userManager.GetAuthenticationTokenAsync(
            user, "Quantira", "RefreshToken");

        var incomingHash = HashToken(request.RefreshToken);

        if (storedHash != incomingHash)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        var tokens = await GenerateTokensAsync(user);

        return Ok(tokens);
    }

    /// <summary>Revokes the current refresh token (logout).</summary>
    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId ?? string.Empty);

        if (user is not null)
            await _userManager.RemoveAuthenticationTokenAsync(
                user, "Quantira", "RefreshToken");

        return NoContent();
    }

    // ── Private helpers ──────────────────────────────────────────────

    private async Task<AuthResponse> GenerateTokensAsync(ApplicationUser user)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        await _userManager.SetAuthenticationTokenAsync(
            user, "Quantira", "RefreshToken", HashToken(refreshToken));

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes));
    }

    private string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email,          user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name,           user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudience = _jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtOptions.Secret)),
            ValidateLifetime = false // Allow expired tokens for refresh.
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, parameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.OrdinalIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}

// ── Request / Response models ────────────────────────────────────────

/// <summary>Registration request body.</summary>
public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password);

/// <summary>Login request body.</summary>
public sealed record LoginRequest(
    string Email,
    string Password);

/// <summary>Token refresh request body.</summary>
public sealed record RefreshRequest(
    string AccessToken,
    string RefreshToken);

/// <summary>Token response returned on successful auth operations.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);

/// <summary>
/// JWT configuration options bound from the "Jwt" section of appsettings.json.
/// </summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Quantira";
    public string Audience { get; set; } = "QuantiraClient";
    public string Secret { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}