using Microsoft.AspNetCore.Mvc.Filters;
using SriKanth.Interface;
using System.Security.Claims;

namespace HRIS.API.Infrastructure.Helpers
{
    public class UserHistoryActionFilter : IAsyncActionFilter
    {
		private readonly IUserHistoryService _userHistoryService;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ILogger<UserHistoryActionFilter> _logger;

		public UserHistoryActionFilter(IUserHistoryService userHistoryService, IHttpContextAccessor httpContextAccessor, ILogger<UserHistoryActionFilter> logger)
		{
			_userHistoryService = userHistoryService;
			_httpContextAccessor = httpContextAccessor;
			_logger = logger;
		}

		public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			var resultContext = await next(); // Execute the action first

			// Only log if the main action succeeded and user is authenticated
			if (resultContext.Exception == null && context.HttpContext.User.Identity?.IsAuthenticated == true)
			{
				// Fire and forget - don't block the main response
				_ = Task.Run(async () =>
				{
					try
					{
						var userId = GetUserIdFromClaims(context.HttpContext.User);
						var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
						var endpoint = context.HttpContext.Request.Path.ToString();
						var actionType = GetActionTypeFromMethod(context.HttpContext.Request.Method);
						var entityType = GetEntityTypeFromController(context.Controller);

						_logger.LogDebug("User history logging - UserId: {UserId}, ActionType: {ActionType}, EntityType: {EntityType}, Endpoint: {Endpoint}",
							userId, actionType, entityType, endpoint);

						// Only log if we have valid data
						if (userId > 0 && !string.IsNullOrEmpty(actionType) && !string.IsNullOrEmpty(entityType))
						{
							await _userHistoryService.LogUserActionAsync(userId, actionType, entityType, endpoint, ipAddress);
						}
						else
						{
							_logger.LogWarning("Skipping user history log due to invalid data - UserId: {UserId}, ActionType: {ActionType}, EntityType: {EntityType}",
								userId, actionType, entityType);
						}
					}
					catch (Exception ex)
					{
						// Log but don't throw - we don't want to affect the main response
						_logger.LogError(ex, "Error occurred while logging user history in background task");
					}
				});
			}
		}

		private int GetUserIdFromClaims(ClaimsPrincipal user)
		{
			try
			{
				var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)
					?? user?.FindFirst("sub")
					?? user?.FindFirst("UserId")
					?? user?.FindFirst("uid");

				if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId) && userId > 0)
				{
					return userId;
				}

				// Debug: Log available claims if UserId not found
				var availableClaims = user?.Claims?.Select(c => $"{c.Type}:{c.Value}").ToList() ?? new List<string>();
				_logger.LogWarning("Could not extract valid UserId. Available claims: {Claims}", string.Join(", ", availableClaims));

				return -1;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error extracting UserId from claims");
				return -1;
			}
		}

		private string GetActionTypeFromMethod(string method)
		{
			return method switch
			{
				"POST" => "Add",
				"PUT" => "Update",
				"PATCH" => "Update",
				"GET" => "View",
				"DELETE" => "Delete",
				_ => "Unknown" // Changed from null to "Unknown"
			};
		}

		private string GetEntityTypeFromController(object controller)
        {
            try
            {
                var controllerName = controller.GetType().Name.Replace("Controller", "");
                return controllerName switch
                {
                    "User" => "User",
                    "Business" => "Business",
                    _ => "General"
                };
            }
			catch (Exception ex)
			{
				return "General";
			}
		}
    }
}
