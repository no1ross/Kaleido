using Kaleido.Process.Participant.Context;

namespace Kaleido.Process.Participant.Planning;

internal interface IExecutionPlanner
{
    ExecutionPlanResult BuildPlan(ParticipantRequest request, ParticipantContext context);
}
