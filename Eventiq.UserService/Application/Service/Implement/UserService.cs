using System.Security.Authentication;
using System.Security.Cryptography;
using AutoMapper;
using Eventiq.Contracts;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Entity;
using AppRoles = Eventiq.UserService.Domain.Enums.AppRoles;
using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Guards;
using Eventiq.UserService.Helper;
using Eventiq.UserService.Model;
using MassTransit;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.UserService.Application.Service;

public class UserService:IUserService
{
    private readonly IJwtService _jwt;
    private readonly IRefreshTokenService _refresh;
    private readonly IUserRepository _userRepository;
    private readonly IBanHistoryRepository _banHistoryRepository;
    private readonly IMapper _mapper;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    
    public UserService(
        IJwtService jwt,
        IRefreshTokenService refresh,
        IUserRepository userRepository,
        IBanHistoryRepository banHistoryRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPublishEndpoint publishEndpoint,
        IMapper mapper)
    {
        _jwt = jwt;
        _refresh = refresh;
        _userRepository = userRepository;
        _banHistoryRepository = banHistoryRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _publishEndpoint = publishEndpoint;
        _mapper = mapper;
    }
    public async Task<LoginResponse> Login(LoginDto dto)
    {
        var user = await _userRepository.GetUserByEmail(dto.Email);
        if (user == null)
            throw new NotFoundException($"User not found with email {dto.Email}");
        if(PasswordHash.SHA256Hash(dto.Password)!=user.PasswordHash)
            throw new InvalidCredentialException("Wrong password");
        UserGuards.EnsureActive(user);
        AppRoles currentRole = RoleGuards.ResolveActiveRole(user);
        var accessToken = _jwt.GenerateAccessToken(
            user.Id, currentRole.ToString(), new Dictionary<string, string>
            {
                ["email"]=user.Email,
            }
        );
        var refreshToken = await _refresh.GenerateRefreshToken(user.Id);
        
        return new LoginResponse(
            accessToken,
            refreshToken
        );
    }

    public async Task<SwitchRoleRepsponse> SwitchRole(Guid userId, Guid organizationId, string? orgName = null)
    {
        var user = await _userRepository.GetUserById(userId);
        if (user == null)
            throw new NotFoundException($"User not found with id {userId}");
        UserGuards.EnsureActive(user);

        var userRole =
            await _userRoleRepository.GetUserRoleByUserIdNOrgId(userId, organizationId);

        RoleGuards.EnsureUserRoleExist(userRole);

        if (!Enum.TryParse<AppRoles>(userRole.Role.Name, out var role))
            throw new BadRequestException($"Invalid role: {userRole.Role.Name}");

        var claims = new Dictionary<string, string>
        {
            ["email"] = user.Email,
            ["orgId"] = organizationId.ToString()
        };
        if (!string.IsNullOrEmpty(orgName))
            claims["orgName"] = orgName;

        var accessToken = _jwt.GenerateAccessToken(user.Id, role.ToString(), claims);
        var oldRefreshToken = await _refresh.GetRefreshTokenModelByUserId(Guid.Parse(user.Id));
        await _refresh.RevokeRefreshToken(oldRefreshToken.Token);
        var newRefreshToken = await _refresh.GenerateRefreshToken(user.Id, organizationId);

        return new SwitchRoleRepsponse(
            accessToken,
            newRefreshToken
        );
    }


    public async Task<RefreshResponse> Refresh(string refreshToken)
    {
        var token = await  _refresh.GetRefreshTokenModel(refreshToken);
        if (! _refresh.ValidateRefreshToken(token))
        {
            await  _refresh.RevokeRefreshToken(refreshToken);
            throw new SecurityTokenException(
                "Invalid refresh token"
            );
        }
        var user = await _userRepository.GetUserById(token.UserId);
        if (user == null)
            throw new NotFoundException($"User not found with id {token.UserId}");
        UserGuards.EnsureActive(user);

        // Determine role & claims based on stored orgId
        var claims = new Dictionary<string, string>
        {
            ["email"] = user.Email,
        };
        Guid? orgIdForNewToken = token.OrganizationId;
        string roleName;

        if (token.OrganizationId.HasValue)
        {
            // Check if the user still has a role in this org
            var userRole = await _userRoleRepository.GetUserRoleByUserIdNOrgId(
                token.UserId, token.OrganizationId.Value);

            if (userRole != null)
            {
                // Org still valid — keep the same org context
                roleName = userRole.Role.Name;
                claims["orgId"] = token.OrganizationId.Value.ToString();
            }
            else
            {
                // Org no longer valid — fallback to basic User role
                roleName = AppRoles.User.ToString();
                orgIdForNewToken = null;
            }
        }
        else
        {
            // No org context — resolve default role
            roleName = RoleGuards.ResolveActiveRole(user).ToString();
        }

        var accessToken = _jwt.GenerateAccessToken(
            token.UserId.ToString(), roleName, claims
        );
        await _refresh.RevokeRefreshToken(refreshToken);
        var newRefreshToken = await _refresh.GenerateRefreshToken(user.Id, orgIdForNewToken);
        return new RefreshResponse(
            accessToken,
            newRefreshToken
        );
    }

    public async Task Logout(string refreshToken)
    {
        await _refresh.RevokeRefreshToken(refreshToken);
    }

    public async Task<bool> Register(RegisterDto dto)
    {
        var isExist = await _userRepository.GetUserByEmail(dto.Email);
        if (isExist!=null)
            throw new BadRequestException($"User with email {dto.Email} already exist");
        var user = _mapper.Map<User>(dto);
        await _userRepository.AddUser(user);
        var userRole = await _roleRepository.GetRoleByName(AppRoles.User.ToString());
        await _userRoleRepository.AddUserRole(new UserRole
        {
            UserId = user.Id,
            RoleId = userRole.Id,
        });
        return true;
    }

    public async Task<UserDto> GetMe(Guid userId, string role, string? orgId = null, string? orgName = null)
    {
        var model = await _userRepository.GetUserById(userId);
        if (model == null)
            throw new NotFoundException($"User not found with id {userId}");
        var rs = _mapper.Map<UserDto>(model);
        rs.CurrentRole = role;
        rs.OrgId = orgId;
        rs.OrgName = orgName;
        return rs;
    }

    public async Task<PaginatedResult<UserResponse>> GetAllUsers(int page, int size, string? email)
    {
        var result = await _userRepository.GetAllUsers(page, size, email);
        return new PaginatedResult<UserResponse>()
        {
            Data = result.Data,
            Page = result.Page,
            Size = result.Size,
            Total = result.Total
        };
    }

    public async Task<bool> BanUser(Guid adminId, Guid userId, BanUserRequest dto)
    {
        var user = await _userRepository.GetTrackingUserById(userId);
        if(user==null)
            throw new NotFoundException($"User not found with id {userId}");
        if(user.IsBanned)
            throw new Exception("User is banned");
        var admin = await _userRepository.GetUserById(adminId);
        UserGuards.EnsureAdmin(admin);
        user.IsBanned = true;
        await  _userRepository.UpdateUser(user);
        await _banHistoryRepository.AddBanHistory(new BanHistory
        {
            Reason = dto.BanReason,
            UserId = userId,
            BannedById = adminId,
        });
        return true;
    }

    public async Task<bool> UnbanUser(Guid adminId, Guid userId)
    {
        var user = await _userRepository.GetTrackingUserById(userId);
        if(user==null)
            throw new NotFoundException($"User not found with id {userId}");
 
        var admin = await _userRepository.GetUserById(adminId);
        UserGuards.EnsureAdmin(admin);
        user.IsBanned = false;
        await  _userRepository.UpdateUser(user);
        await _banHistoryRepository.AddBanHistory(new BanHistory
        {
            Reason = "Unban",
            UserId = userId,
            BannedById = adminId,
        });
        return true;
    }

    public async Task<UserDto> ChangePassword(Guid userId, ChangePasswordRequest dto)
    {
        var user = await _userRepository.GetTrackingUserById(userId);
        if (user == null)
            throw new NotFoundException($"User not found with id {userId}");
        if(PasswordHash.SHA256Hash(dto.CurrentPassword)!=user.PasswordHash)
            throw new SecurityTokenException("Invalid password");
        user.PasswordHash = PasswordHash.SHA256Hash(dto.NewPassword);
        await _userRepository.UpdateUser(user);
        return _mapper.Map<UserDto>(user);
    }

    public async Task ForgotPassword(string email)
    {
        var user = await _userRepository.GetUserByEmail(email);
        if (user == null)
            throw new NotFoundException($"User not found with email {email}");
        
        // Remove old tokens for this user
        await _passwordResetTokenRepository.RemoveTokensByUserId(Guid.Parse(user.Id));

        // Generate a new token
        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expires = DateTime.UtcNow.AddMinutes(30);

        var passwordResetToken = new PasswordResetToken
        {
            Token = resetToken,
            Expires = expires,
            UserId = Guid.Parse(user.Id),
        };

        await _passwordResetTokenRepository.AddToken(passwordResetToken);

        // Publish event to EmailService
        await _publishEndpoint.Publish(new PasswordResetRequested
        {
            EmailAddress = email,
            ResetToken = resetToken,
            ExpireAt = expires
        });
    }

    public async Task<bool> ResetPassword(string token, string newPassword)
    {
        var resetToken = await _passwordResetTokenRepository.GetByToken(token);
        if (resetToken == null)
            throw new NotFoundException("Invalid or expired reset token");
        
        if (DateTime.UtcNow > resetToken.Expires)
        {
            await _passwordResetTokenRepository.RemoveToken(resetToken);
            throw new BadRequestException("Reset token has expired");
        }

        var user = await _userRepository.GetTrackingUserById(resetToken.UserId);
        if (user == null)
            throw new NotFoundException($"User not found");

        user.PasswordHash = PasswordHash.SHA256Hash(newPassword);
        await _userRepository.UpdateUser(user);

        // Clean up used token
        await _passwordResetTokenRepository.RemoveToken(resetToken);

        return true;
    }

    public async Task<SwitchRoleRepsponse> SwitchToUser(Guid userId)
    {
        var user = await _userRepository.GetUserById(userId);
        if (user == null)
            throw new NotFoundException($"User not found with id {userId}");
        UserGuards.EnsureActive(user);

        var claims = new Dictionary<string, string>
        {
            ["email"] = user.Email,
        };

        var accessToken = _jwt.GenerateAccessToken(user.Id, AppRoles.User.ToString(), claims);
        var oldRefreshToken = await _refresh.GetRefreshTokenModelByUserId(Guid.Parse(user.Id));
        if (oldRefreshToken != null)
            await _refresh.RevokeRefreshToken(oldRefreshToken.Token);
        var newRefreshToken = await _refresh.GenerateRefreshToken(user.Id);

        return new SwitchRoleRepsponse(
            accessToken,
            newRefreshToken
        );
    }

}