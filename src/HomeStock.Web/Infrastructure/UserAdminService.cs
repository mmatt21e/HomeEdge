using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomeStock.Web.Infrastructure;

public record UserAdminDto(
    string Id,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool IsLockedOut,
    bool EmailConfirmed,
    DateTime CreatedAt);

/// <summary>Administrator operations over Identity users: roles, enable/disable, create, reset, delete.</summary>
public interface IUserAdminService
{
    Task<IReadOnlyList<UserAdminDto>> GetUsersAsync(CancellationToken ct = default);
    Task<Result> SetRoleAsync(string userId, string role, bool inRole);
    Task<Result> SetActiveAsync(string userId, bool active);
    Task<Result> CreateUserAsync(string email, string password, string role);
    Task<Result> ResetPasswordAsync(string userId, string newPassword);
    Task<Result> DeleteUserAsync(string userId);
}

public class UserAdminService(
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser) : IUserAdminService
{
    public async Task<IReadOnlyList<UserAdminDto>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await userManager.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var list = new List<UserAdminDto>();
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            var lockedOut = u.LockoutEnd is { } end && end > DateTimeOffset.UtcNow;
            list.Add(new UserAdminDto(u.Id, u.Email, u.DisplayName, roles.ToList(), u.IsActive, lockedOut, u.EmailConfirmed, u.CreatedAt));
        }
        return list;
    }

    public async Task<Result> SetRoleAsync(string userId, string role, bool inRole)
    {
        if (!Roles.All.Contains(role)) return Result.Failure("Unknown role.");
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Result.Failure("User not found.");

        // Guard: never remove the last administrator.
        if (!inRole && role == Roles.Administrator && await IsLastAdminAsync(user))
            return Result.Failure("You cannot remove the last administrator.");

        var isInRole = await userManager.IsInRoleAsync(user, role);
        if (inRole && !isInRole) await userManager.AddToRoleAsync(user, role);
        else if (!inRole && isInRole) await userManager.RemoveFromRoleAsync(user, role);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(string userId, bool active)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Result.Failure("User not found.");
        if (!active && user.Id == currentUser.UserId) return Result.Failure("You cannot disable your own account.");
        if (!active && await IsLastAdminAsync(user)) return Result.Failure("You cannot disable the last administrator.");

        user.IsActive = active;
        await userManager.SetLockoutEnabledAsync(user, true);
        // A far-future lockout end blocks sign-in; null re-enables it.
        await userManager.SetLockoutEndDateAsync(user, active ? null : DateTimeOffset.MaxValue);
        await userManager.UpdateAsync(user);
        return Result.Success();
    }

    public async Task<Result> CreateUserAsync(string email, string password, string role)
    {
        email = email.Trim();
        if (string.IsNullOrWhiteSpace(email)) return Result.Failure("Email is required.");
        if (await userManager.FindByEmailAsync(email) is not null) return Result.Failure("A user with that email already exists.");
        if (!Roles.All.Contains(role)) return Result.Failure("Unknown role.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,      // admin-created accounts are trusted
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) return Result.Failure(result.Errors.Select(e => e.Description).ToList());

        await userManager.AddToRoleAsync(user, role);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string userId, string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Result.Failure("User not found.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Description).ToList());
    }

    public async Task<Result> DeleteUserAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Result.Failure("User not found.");
        if (user.Id == currentUser.UserId) return Result.Failure("You cannot delete your own account.");
        if (await IsLastAdminAsync(user)) return Result.Failure("You cannot delete the last administrator.");

        var result = await userManager.DeleteAsync(user);
        return result.Succeeded ? Result.Success() : Result.Failure(result.Errors.Select(e => e.Description).ToList());
    }

    private async Task<bool> IsLastAdminAsync(ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, Roles.Administrator)) return false;
        var admins = await userManager.GetUsersInRoleAsync(Roles.Administrator);
        return admins.Count <= 1;
    }
}
