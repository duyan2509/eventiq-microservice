using Eventiq.Contracts.Grpc;
using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Domain.Enum;
using Eventiq.OrganizationService.Domain.Repositories;
using Grpc.Core;

namespace Eventiq.OrganizationService.Grpc;

public class OrgInternalGrpcService : OrgInternal.OrgInternalBase
{
    private readonly IOrganizationRepository _orgRepo;
    private readonly IPlatformConfigService _configService;

    public OrgInternalGrpcService(
        IOrganizationRepository orgRepo,
        IPlatformConfigService configService)
    {
        _orgRepo = orgRepo;
        _configService = configService;
    }

    public override async Task<PaymentStatusInfo> GetPaymentStatus(
        GetPaymentStatusRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrgId, out var orgId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid org_id"));

        var org = await _orgRepo.GetByIdAsync(orgId, context.CancellationToken);
        if (org == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Organization {orgId} not found"));

        return new PaymentStatusInfo
        {
            IsActive = org.PaymentStatus == PaymentStatus.Configured,
            StripeAccountId = org.StripeAccountId ?? string.Empty
        };
    }

    public override async Task<PlatformConfigInfo> GetPlatformConfig(
        PlatformConfigRequest request, ServerCallContext context)
    {
        var config = await _configService.GetInternalAsync(context.CancellationToken);
        return new PlatformConfigInfo
        {
            CurrentFeeRate = (double)config.CurrentFeeRate,
            PayoutDayOfMonth = config.PayoutDayOfMonth
        };
    }
}
