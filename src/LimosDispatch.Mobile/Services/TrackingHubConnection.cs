using LimosDispatch.Core.Constants;
using Microsoft.AspNetCore.SignalR.Client;
namespace LimosDispatch.Mobile.Services;
public sealed class TrackingHubOptions
{
    public Uri BaseUri { get; init; } = new(ApiRoutes.BaseUrl);
}
internal static class TrackingHubConnection
{
    internal static string Scope(AuthService auth) => auth.User is { } user ? user.Company + "|" + user.Id : throw new UnauthorizedAccessException("Sign in to your company workspace.");
    internal static HubConnection Create(AuthService auth,TrackingHubOptions options)
    {
        var scope=Scope(auth);
        return new HubConnectionBuilder().WithUrl(new Uri(options.BaseUri,"hubs/tracking"),o=>o.AccessTokenProvider=async()=>
        {
            if(Scope(auth)!=scope)throw new UnauthorizedAccessException("Your session changed. Sign in again.");
            return await auth.GetTokenAsync();
        }).WithAutomaticReconnect().Build();
    }
}
