using Microsoft.AspNetCore.Identity;
using StockTracker.App.Models;

namespace StockTracker.App.Services;

public class DevUserService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private string? _devUserId;

    public DevUserService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<string> GetDevUserIdAsync()
    {
        if (_devUserId != null) return _devUserId;
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync("dev@local");
        _devUserId = user?.Id ?? "";
        return _devUserId;
    }
}
