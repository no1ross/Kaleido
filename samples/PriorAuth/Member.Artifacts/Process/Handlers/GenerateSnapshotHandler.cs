using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Member.Data;
using Kaleido.Samples.PriorAuth.Member.Process.Responses;
using Kaleido.Samples.PriorAuth.Member.Process.Steps;
using Kaleido.Samples.PriorAuth.Member.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Kaleido.Samples.PriorAuth.Member.Process.Handlers;

public sealed class GenerateSnapshotHandler(
    MemberDbContext dbContext)
    : IProcessStepHandler<GenerateSnapshotStep, GenerateSnapshotResponse>
{
    public async Task<ProcessStepHandlerResult<GenerateSnapshotResponse>> ExecuteAsync(
        GenerateSnapshotStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var enrollment =
            await dbContext.MemberEnrollments
                .AsNoTracking()
                .Include(x => x.Member)
                .Include(x => x.Address)
                .SingleAsync(
                    x => x.MemberEnrollmentId == processStep.MemberEnrollmentId
                        && x.MemberId == processStep.MemberId,
                    cancellationToken);

        var capturedUtc =
            DateTimeOffset.UtcNow;

        var snapshotView =
            new MemberSnapshotView
            {
                MemberId = enrollment.MemberId,
                MemberEnrollmentId = enrollment.MemberEnrollmentId,
                MemberNumber = enrollment.Member.MemberNumber,
                DisplayName = enrollment.Member.FirstName + " " + enrollment.Member.LastName,
                DateOfBirth = enrollment.Member.DateOfBirth,
                IssuanceState = enrollment.Address.State,
                AddressLine1 = enrollment.Address.AddressLine1,
                AddressLine2 = enrollment.Address.AddressLine2,
                City = enrollment.Address.City,
                AddressState = enrollment.Address.State,
                PostalCode = enrollment.Address.PostalCode,
                LineOfBusiness = enrollment.LineOfBusiness,
                PlanId = enrollment.PlanId,
                PlanName = enrollment.PlanName,
                EffectiveDate = enrollment.EffectiveDate,
                TerminationDate = enrollment.TerminationDate,
                CapturedUtc = capturedUtc
            };

        var snapshot =
            new MemberSnapshot
            {
                MemberSnapshotId = Guid.NewGuid(),
                MemberId = enrollment.MemberId,
                MemberEnrollmentId = enrollment.MemberEnrollmentId,
                SchemaVersion = 1,
                CapturedUtc = capturedUtc,
                SnapshotJson = JsonSerializer.Serialize(snapshotView)
            };

        dbContext.MemberSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessStepHandlerResult<GenerateSnapshotResponse>
        {
            Response = new GenerateSnapshotResponse
            {
                MemberSnapshotId = snapshot.MemberSnapshotId
            }
        };
    }
}
