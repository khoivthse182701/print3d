using MediatR;
using TheBoys.Application.Common.Models;

namespace TheBoys.Application.Approvals.Commands.CreateCustomerApproval;

public sealed record CreateCustomerApprovalCommand(
    Guid UserId,
    Guid VersionId,
    object Configuration,
    string? PolicyVersion = null
) : IRequest<Result<CustomerApprovalDto>>;

