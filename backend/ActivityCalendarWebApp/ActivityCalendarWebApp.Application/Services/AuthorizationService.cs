using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ActivityCalendarWebApp.Application.DTOs.Authorization;
using ActivityCalendarWebApp.Application.Interfaces;
using ActivityCalendarWebApp.Domain.Entities;
using ActivityCalendarWebApp.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ActivityCalendarWebApp.Application.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthorizationService(IUnitOfWork unitOfWork, ITokenService tokenService, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }
    
    public async Task RegisterAsync(RegisterViewModel model)
    {
        var user = await _unitOfWork.Users.GetUserByUsernameAsync(model.Username);
        if (user != null) throw new Exception("Conflict User");
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Username = model.Username,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(model.Password),
        };

        await _unitOfWork.Users.AddUserAsync(newUser);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<LoginResponseViewModel> LoginAsync(LoginViewModel model)
    {
        var user = await _unitOfWork.Users.GetUserByUsernameAsync(model.Username);
        if (user != null && BCrypt.Net.BCrypt.EnhancedVerify(model.Password, user.PasswordHash))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, model.Username),
                new Claim("UserId", user.Id.ToString())
            };

            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.Now.AddHours(double.Parse(_configuration["Jwt:RefreshTokenExpiration"]));
            _unitOfWork.Users.UpdateUser(user);
            await _unitOfWork.SaveChangesAsync();
            var response = new LoginResponseViewModel(accessToken, refreshToken);
            _httpContextAccessor.HttpContext.Response.Cookies.Append("tasty-cookies", response.AccessToken, new CookieOptions { HttpOnly = true });
            return response;
        }
        throw new UnauthorizedAccessException("Invalid username or password");
    }

    public async Task LogoutAsync()
    {
        var userId = GetCurrentUserId();
        var user = await _unitOfWork.Users.GetUserByIdAsync(userId);

        if (user is { RefreshToken: not null })
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            _unitOfWork.Users.UpdateUser(user);
            await _unitOfWork.SaveChangesAsync();
        }

        _httpContextAccessor.HttpContext.Response.Cookies.Delete("tasty-cookies");
    }


    public async Task<LoginResponseViewModel> RefreshAsync(RefreshTokenViewModel model)
    {
        var principal = GetTokenPrincipals(model.AccessToken);
        if (principal?.Identity?.Name is null) throw new UnauthorizedAccessException("Invalid token");

        var identityUser = await _unitOfWork.Users.GetUserByUsernameAsync(principal.Identity.Name);
        if (identityUser is null || identityUser.RefreshToken != model.RefreshToken || identityUser.RefreshTokenExpiry < DateTime.Now)
            throw new UnauthorizedAccessException("Invalid token");
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, identityUser.Username),
            new Claim("UserId", identityUser.Id.ToString())
        };

        var accessToken = _tokenService.GenerateAccessToken(claims);
        var refreshToken = _tokenService.GenerateRefreshToken();
        identityUser.RefreshToken = refreshToken;
        identityUser.RefreshTokenExpiry = DateTime.Now.AddHours(double.Parse(_configuration["Jwt:RefreshTokenExpiration"]));
        _unitOfWork.Users.UpdateUser(identityUser);
        await _unitOfWork.SaveChangesAsync();

        var response = new LoginResponseViewModel(accessToken, refreshToken);
        _httpContextAccessor.HttpContext.Response.Cookies.Append("tasty-cookies", response.AccessToken, new CookieOptions { HttpOnly = true });
        return response;
    }
    
    private ClaimsPrincipal? GetTokenPrincipals(string jwtAccess)
    {
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]))
        };

        return new JwtSecurityTokenHandler().ValidateToken(jwtAccess, validation, out _);
    }
    
    private Guid GetCurrentUserId()
    {
        if (_httpContextAccessor.HttpContext == null)
            throw new UnauthorizedAccessException("HTTP context is not available.");

        var userIdClaim = _httpContextAccessor.HttpContext.User.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("User is not authorized or UserId is invalid.");

        return userId;
    }
}