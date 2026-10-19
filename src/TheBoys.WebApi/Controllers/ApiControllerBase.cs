using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    protected bool IsManagement => User.IsInRole(Roles.Staff) || User.IsInRole(Roles.Admin);
    protected bool CanAccess(Guid ownerId) => ownerId == UserId || IsManagement;

    protected IActionResult ProblemResult(TheBoys.Application.Common.Models.Error error) => error.Type switch
    {
        TheBoys.Application.Common.Models.ErrorType.Validation => BadRequest(new { message = error.Message }),
        TheBoys.Application.Common.Models.ErrorType.NotFound => string.IsNullOrEmpty(error.Message) ? NotFound() : NotFound(new { message = error.Message }),
        TheBoys.Application.Common.Models.ErrorType.Conflict when error.Extra != null => Conflict(new { message = error.Message, quote = error.Extra }),
        TheBoys.Application.Common.Models.ErrorType.Conflict => Conflict(new { message = error.Message }),
        TheBoys.Application.Common.Models.ErrorType.Unauthorized => Unauthorized(new { message = error.Message }),
        TheBoys.Application.Common.Models.ErrorType.PaymentRequired => StatusCode(StatusCodes.Status402PaymentRequired, new { message = error.Message }),
        TheBoys.Application.Common.Models.ErrorType.ServiceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = error.Message }),
        TheBoys.Application.Common.Models.ErrorType.BadGateway => StatusCode(StatusCodes.Status502BadGateway, new { message = error.Message }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message })
    };
}
