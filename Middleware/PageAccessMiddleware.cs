using System.Security.Claims;

namespace StudentManagement.Middleware;

public class PageAccessMiddleware
{
    private readonly RequestDelegate next;

    public PageAccessMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

        if ((path is "/" or "/index.html") && !isAuthenticated)
        {
            context.Response.Redirect("/login.html");
            return;
        }

        if (path is "/admin.html" && (!isAuthenticated || role != "Admin"))
        {
            context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
            return;
        }

        if (path is "/doctor.html" && (!isAuthenticated || role != "Doctor"))
        {
            context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
            return;
        }

        if (path is "/user.html" && (!isAuthenticated || role != "User"))
        {
            context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
            return;
        }

        if (path is "/support.html" && (!isAuthenticated || role is not ("Support" or "Admin")))
        {
            context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
            return;
        }

        if ((path is "/" or "/index.html") && role is not ("Receptionist" or "Admin"))
        {
            context.Response.Redirect(role == "Doctor" ? "/doctor.html" : "/user.html");
            return;
        }

        await next(context);
    }
}
