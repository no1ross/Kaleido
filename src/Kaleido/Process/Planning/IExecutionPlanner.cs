using Kaleido.Process.Context;

namespace Kaleido.Process.Planning;

internal interface IExecutionPlanner
{
    ExecutionPlanResult BuildPlan(ProcessorRequest request, ProcessorContext context);
}
