using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Sizzle.WebApi.Hubs;

[Authorize]
public class RecipeNotificationsHub : Hub;