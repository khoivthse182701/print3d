using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    protected bool IsManagement => User.IsInRole(Roles.Staff) || User.IsInRole(Roles.Admin);
    protected bool CanAccess(Guid ownerId) => ownerId == UserId || IsManagement;
}
