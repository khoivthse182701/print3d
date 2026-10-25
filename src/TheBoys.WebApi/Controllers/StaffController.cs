using System.Globalization;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Staff.Queries.GetStaffGenerationJobs;
using TheBoys.Application.Staff.Queries.GetStaffOrders;
using TheBoys.Domain.Enums;
using TheBoys.WebApi.Contracts.Staff;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

[ApiController]
[Route("api/v2/staff")]
[Authorize(Roles = Roles.Management)]
public sealed class StaffController(ISender mediator) : ApiControllerBase
{
    private static readonly Regex IsoDateTimeWithTimezoneRegex = new(
        @"^(\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?)(Z|[+-]\d{2}(?::?\d{2})?)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [HttpGet("generation-jobs")]
    public async Task<IActionResult> GetGenerationJobs([FromQuery] StaffGenerationJobsQueryParameters? parameters, CancellationToken ct)
    {
        parameters ??= new StaffGenerationJobsQueryParameters();
        var capturedUtcNow = DateTime.UtcNow;

        if (!ValidatePagingAndSort(parameters.Page, parameters.PageSize, parameters.Sort,
            out var page, out var pageSize, out var sort, out var pagingErrorResult))
        {
            return pagingErrorResult!;
        }

        GenerationStatus? status = null;
        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            if (!TryParseEnum<GenerationStatus>(parameters.Status, out var parsedStatus))
            {
                return ValidationProblem("Tham số 'status' không hợp lệ cho tác vụ tạo 3D.", "InvalidStatus");
            }
            status = parsedStatus;
        }

        PrintReviewStatus? reviewStatus = null;
        if (!string.IsNullOrWhiteSpace(parameters.ReviewStatus))
        {
            if (!TryParseEnum<PrintReviewStatus>(parameters.ReviewStatus, out var parsedReviewStatus))
            {
                return ValidationProblem("Tham số 'reviewStatus' không hợp lệ.", "InvalidReviewStatus");
            }
            reviewStatus = parsedReviewStatus;
        }

        string? provider = null;
        if (parameters.Provider != null)
        {
            if (parameters.Provider.Length > 30)
            {
                return ValidationProblem("Tham số 'provider' không được vượt quá 30 ký tự.", "ProviderTooLong");
            }
            if (!string.IsNullOrWhiteSpace(parameters.Provider))
            {
                provider = parameters.Provider.Trim();
            }
        }

        if (!ValidateDateAndAgeFilters(
            parameters.CreatedFromUtc, parameters.CreatedToUtc, parameters.MinAgeMinutes,
            out var fromUtc, out var toUtc, out var minAgeMinutes, out var dateErrorResult))
        {
            return dateErrorResult!;
        }

        var query = new GetStaffGenerationJobsQuery(
            status,
            reviewStatus,
            provider,
            fromUtc,
            toUtc,
            minAgeMinutes,
            page,
            pageSize,
            sort,
            capturedUtcNow
        );

        var result = await mediator.Send(query, ct);

        var response = new StaffPagedResponse<StaffGenerationJobResponse>(
            result.Items.Select(x => new StaffGenerationJobResponse(
                x.Id,
                x.ModelName,
                x.Provider,
                x.Status,
                x.Stage,
                x.Progress,
                x.ReviewStatus,
                DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc),
                AsUtc(x.CompletedAtUtc),
                x.AgeMinutes
            )).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount
        );

        return Ok(response);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] StaffOrdersQueryParameters? parameters, CancellationToken ct)
    {
        parameters ??= new StaffOrdersQueryParameters();
        var capturedUtcNow = DateTime.UtcNow;

        if (!ValidatePagingAndSort(parameters.Page, parameters.PageSize, parameters.Sort,
            out var page, out var pageSize, out var sort, out var pagingErrorResult))
        {
            return pagingErrorResult!;
        }

        OrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            if (!TryParseEnum<OrderStatus>(parameters.Status, out var parsedStatus))
            {
                return ValidationProblem("Tham số 'status' không hợp lệ cho đơn hàng.", "InvalidStatus");
            }
            status = parsedStatus;
        }

        string? orderCode = null;
        if (parameters.OrderCode != null)
        {
            if (parameters.OrderCode.Length > 50)
            {
                return ValidationProblem("Tham số 'orderCode' không được vượt quá 50 ký tự.", "OrderCodeTooLong");
            }
            if (!string.IsNullOrWhiteSpace(parameters.OrderCode))
            {
                orderCode = parameters.OrderCode.Trim();
            }
        }

        if (!ValidateDateAndAgeFilters(
            parameters.CreatedFromUtc, parameters.CreatedToUtc, parameters.MinAgeMinutes,
            out var fromUtc, out var toUtc, out var minAgeMinutes, out var dateErrorResult))
        {
            return dateErrorResult!;
        }

        var query = new GetStaffOrdersQuery(
            status,
            orderCode,
            fromUtc,
            toUtc,
            minAgeMinutes,
            page,
            pageSize,
            sort,
            capturedUtcNow
        );

        var result = await mediator.Send(query, ct);

        var response = new StaffPagedResponse<StaffOrderResponse>(
            result.Items.Select(x => new StaffOrderResponse(
                x.Id,
                x.OrderCode,
                x.GenerationTaskId,
                x.Status,
                x.Quantity,
                DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc),
                x.AgeMinutes,
                AsUtc(x.PaidAtUtc),
                AsUtc(x.SentToPartnerAtUtc),
                AsUtc(x.PrintingStartedAtUtc),
                AsUtc(x.PackagedAtUtc),
                AsUtc(x.ShippedAtUtc),
                AsUtc(x.DeliveredAtUtc),
                AsUtc(x.CancelledAtUtc)
            )).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount
        );

        return Ok(response);
    }

    private static DateTime? AsUtc(DateTime? dt) => dt.HasValue ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : null;

    private bool ValidatePagingAndSort(
        string? pageRaw,
        string? pageSizeRaw,
        string? sortRaw,
        out int page,
        out int pageSize,
        out string sort,
        out IActionResult? errorResult)
    {
        page = 1;
        pageSize = 20;
        sort = "oldest";
        errorResult = null;

        if (!string.IsNullOrWhiteSpace(pageRaw))
        {
            if (!int.TryParse(pageRaw, out page) || page < 1 || page > 100_000)
            {
                errorResult = ValidationProblem("Tham số 'page' phải là số nguyên từ 1 đến 100000.", "InvalidPage");
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(pageSizeRaw))
        {
            if (!int.TryParse(pageSizeRaw, out pageSize) || pageSize < 1 || pageSize > 100)
            {
                errorResult = ValidationProblem("Tham số 'pageSize' phải là số nguyên từ 1 đến 100.", "InvalidPageSize");
                return false;
            }
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            errorResult = ValidationProblem("Chỉ số phân trang quá lớn gây tràn bộ đệm.", "PageOverflow");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sortRaw))
        {
            var s = sortRaw.Trim().ToLowerInvariant();
            if (s != "oldest" && s != "newest")
            {
                errorResult = ValidationProblem("Tham số 'sort' chỉ chấp nhận 'oldest' hoặc 'newest'.", "InvalidSort");
                return false;
            }
            sort = s;
        }

        return true;
    }

    private bool ValidateDateAndAgeFilters(
        string? createdFromRaw,
        string? createdToRaw,
        string? minAgeMinutesRaw,
        out DateTime? fromUtc,
        out DateTime? toUtc,
        out int? minAgeMinutes,
        out IActionResult? errorResult)
    {
        fromUtc = null;
        toUtc = null;
        minAgeMinutes = null;
        errorResult = null;

        if (!string.IsNullOrWhiteSpace(createdFromRaw))
        {
            if (!TryParseUtcDate(createdFromRaw, "createdFromUtc", out var parsedFrom, out var error))
            {
                errorResult = ValidationProblem(error!, "InvalidCreatedFromUtc");
                return false;
            }
            fromUtc = parsedFrom;
        }

        if (!string.IsNullOrWhiteSpace(createdToRaw))
        {
            if (!TryParseUtcDate(createdToRaw, "createdToUtc", out var parsedTo, out var error))
            {
                errorResult = ValidationProblem(error!, "InvalidCreatedToUtc");
                return false;
            }
            toUtc = parsedTo;
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value >= toUtc.Value)
        {
            errorResult = ValidationProblem("Tham số 'createdFromUtc' phải nhỏ hơn 'createdToUtc'.", "InvalidDateRange");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(minAgeMinutesRaw))
        {
            if (!int.TryParse(minAgeMinutesRaw, out var age) || age < 0)
            {
                errorResult = ValidationProblem("Tham số 'minAgeMinutes' phải là số nguyên không âm.", "InvalidMinAgeMinutes");
                return false;
            }
            minAgeMinutes = age;
        }

        return true;
    }

    private static bool TryParseEnum<T>(string value, out T result) where T : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();

        // Chuỗi chứa dấu phẩy hoặc khoảng trắng là dạng danh sách/composite, không hợp lệ cho single enum
        if (trimmed.Contains(',') || trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        // Kiểm tra nếu là dạng số nguyên định nghĩa hợp lệ
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            if (Enum.IsDefined(typeof(T), intValue))
            {
                result = (T)Enum.ToObject(typeof(T), intValue);
                return true;
            }
            return false;
        }

        // Kiểm tra nếu là tên chuỗi enum đơn (không phân biệt hoa thường)
        foreach (var name in Enum.GetNames<T>())
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                result = Enum.Parse<T>(name);
                return true;
            }
        }

        return false;
    }

    private static bool TryParseUtcDate(string input, string paramName, out DateTime utcDate, out string? errorMessage)
    {
        utcDate = default;
        errorMessage = null;

        var trimmed = input.Trim();
        if (!IsoDateTimeWithTimezoneRegex.IsMatch(trimmed))
        {
            errorMessage = $"Tham số '{paramName}' phải có định dạng ngày giờ ISO-8601 kèm múi giờ rõ ràng (ví dụ: '2026-10-25T10:00:00Z' hoặc '2026-10-25T17:00:00+07:00').";
            return false;
        }

        if (!DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            errorMessage = $"Tham số '{paramName}' chứa giá trị ngày giờ không hợp lệ.";
            return false;
        }

        utcDate = dto.UtcDateTime;
        return true;
    }

    private IActionResult ValidationProblem(string detail, string code)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return BadRequest(problem);
    }
}
