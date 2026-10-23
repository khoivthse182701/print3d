using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using TheBoys.Application.Quotes;
using TheBoys.Application.Quotes.Commands.CreateQuote;
using TheBoys.Application.Quotes.Queries;

namespace TheBoys.WebApi.Controllers.v2;

public sealed class CreateQuoteRequest
{
    public Guid VersionId { get; set; }
    public object Configuration { get; set; } = default!;
    public Guid PricingPackageId { get; set; }
    [Range(1, 10)] public int Quantity { get; set; } = 1;
    public ShippingAddressDto Address { get; set; } = default!;
}

[ApiController, Route("api/v2/quotes"), Authorize]
public sealed class QuotesController(ISender mediator) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateQuoteRequest request, CancellationToken ct)
    {
        var command = new CreateQuoteCommand(
            UserId,
            request.VersionId,
            request.Configuration,
            request.PricingPackageId,
            request.Quantity,
            request.Address
        );

        var result = await mediator.Send(command, ct);
        return result.IsFailure
            ? ProblemResult(result.Error)
            : CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetQuoteByIdQuery(id, UserId, IsManagement), ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var quotes = await mediator.Send(new GetQuotesQuery(UserId, IsManagement, page), ct);
        return Ok(quotes);
    }
}

