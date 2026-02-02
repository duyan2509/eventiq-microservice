using System.Security.Authentication;
using AutoMapper;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Enums;
using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Guards;
using Eventiq.UserService.Helper;
using Eventiq.UserService.Model;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.UserService.Application.Service;

public class UserService:IUserService
{
    private readonly IJwtService _jwt;
    private readonly IRefreshTokenService _refresh;
    private readonly IUserRepository _userRepository;
    private readonly IBanHistoryRepository _banHistoryRepository;
    private readonly IMapper _mapper;

    public UserService(
        IJwtService jwt,
        IRefreshTokenService refresh,
        IUserRepository userRepository,
        IBanHistoryRepository banHistoryRepository,
        IMapper mapper)
    {
        _jwt = jwt;
        _refresh = refresh;
        _userRepository = userRepository;
        _banHistoryRepository = banHistoryRepository;
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

        var accessToken = _jwt.GenerateAccessToken(
            user.Id, RoleGuards.ResolveActiveRole(user), new Dictionary<string, string>
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

    public async Task<SwitchRoleRepsponse> SwitchRole(Guid userId, AppRoles role)
    {
        var user = await _userRepository.GetUserById(userId);
        if (user == null)
            throw new NotFoundException($"User not found with email {user.Id}");
        UserGuards.EnsureActive(user);
        UserGuards.EnsureHasRole(user, role);
        
        
        var accessToken = _jwt.GenerateAccessToken(
            user.Id, RoleGuards.ResolveActiveRole(user), new Dictionary<string, string>
            {
                ["email"]=user.Email,
            }
        );
        var refreshToken = await _refresh.GenerateRefreshToken(user.Id);
        
        return new SwitchRoleRepsponse(
            accessToken,
            refreshToken
        );
    }

    public async Task<RefreshResponse> Refresh(string refreshToken)
    {
        var token = await  _refresh.GetRefreshTokenModel(refreshToken);
        if (! _refresh.ValidateRefreshToken(token))
        {
            _refresh.RevokeRefreshToken(refreshToken);
            throw new SecurityTokenException(
                "Invalid refresh token"
            );
        }
        var user = await _userRepository.GetUserById(token.UserId);
        if (user == null)
            throw new NotFoundException($"User not found with id {token.UserId}");
        UserGuards.EnsureActive(user);
        var accessToken = _jwt.GenerateAccessToken(
            token.UserId.ToString(), RoleGuards.ResolveActiveRole(user), new Dictionary<string, string>
            {
                ["email"]=user.Email,
            }
        );
        _refresh.RevokeRefreshToken(refreshToken);
        var newRefreshToken = await  _refresh.GenerateRefreshToken(user.Id);
        return new RefreshResponse(
            accessToken,
            newRefreshToken
        );
    }

    public void Logout(string refreshToken)
    {
        _refresh.RevokeRefreshToken(refreshToken);
    }

    public async Task<RegisterDto> Register(RegisterDto dto)
    {
        var user = _mapper.Map<User>(dto);
        await _userRepository.AddUser(user);
        return _mapper.Map<RegisterDto>(user);
    }

    public async Task<UserDto> GetMe(Guid userId)
    {
        var model = await _userRepository.GetUserById(userId);
        if (model == null)
            throw new NotFoundException($"User not found with id {userId}");
        return _mapper.Map<UserDto>(model);
    }

    public async Task<PaginatedResult<UserDto>> GetAllUsers(int page, int size, string email)
    {
        var result = await _userRepository.GetAllUsers(page, size, email);
        var users = _mapper.Map<List<UserDto>>(result);
        return new PaginatedResult<UserDto>()
        {
            Data = users,
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
        if(user.IsBanned)
            throw new Exception("User is banned");
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


}