using Microsoft.AspNetCore.Mvc.Filters;
using SriKanth.Interface;
using System.Security.Claims;

namespace HRIS.API.Infrastructure.Helpers
{
    public class UserHistoryActionFilter : IAsyncActionFilter
    {
        private readonly IUserHistoryService _userHistoryService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserHistoryActionFilter(IUserHistoryService userHistoryService, IHttpContextAccessor httpContextAccessor)
        {
            _userHistoryService = userHistoryService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var resultContext = await next(); // Execute the action

            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var userId = GetUserIdFromClaims(context.HttpContext.User);
                var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
                var endpoint = context.HttpContext.Request.Path;
                var actionType = GetActionTypeFromMethod(context.HttpContext.Request.Method);
                var entityType = GetEntityTypeFromController(context.Controller);

                if (!string.IsNullOrEmpty(actionType) && !string.IsNullOrEmpty(entityType))
                {
                    await _userHistoryService.LogUserActionAsync(userId, actionType, entityType, endpoint, ipAddress);
                }
            }
        }

		private int GetUserIdFromClaims(ClaimsPrincipal user)
		{
			var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier) ?? user?.FindFirst("sub");
			return userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId) ? userId : -1;
		}


		private string GetActionTypeFromMethod(string method)
        {
            return method switch
            {
                "POST" => "Add",
                "PUT" => "Update",
                "GET" => "View",
                _ => null
            };
        }

		private string GetEntityTypeFromController(object controller)
		{
			var controllerName = controller.GetType().Name.Replace("Controller", "");

			return controllerName switch
			{
				"Business" => "Business",
				"User" => "User",
				_ => "General"
			};
		}
	}
}
