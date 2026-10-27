namespace Tracker.Domain;

public static class TaskRequestWorkflow
{
    // Missing or ineligible tasks stop this chain before scoring or state changes.
    public static Result<PlanningState.Confirmed> Request(
        IReadOnlyList<TaskItem> tasks, string? id, DateOnly date, int availableMinutes,
        Func<TaskItem, int> scorePolicy) =>
        TaskLookup.Required(tasks, id)
            .Bind(task => CheckEligibility(task, date))
            .Map(task => TaskPipeline.CreatePlan([task], date, 1, scorePolicy))
            .Bind(plan => PrepareAndConfirm(PlanningState.Start(plan, availableMinutes)));

    public static Result<PlanningState.Confirmed> PrepareAndConfirm(PlanningState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return PlanningWorkflow.Prepare(state) switch
        {
            PlanningTransition.Changed { Next: PlanningState.Prepared prepared } =>
                ConfirmPrepared(prepared),
            PlanningTransition.Rejected rejected =>
                Result<PlanningState.Confirmed>.Fail("Preparation rejected: " + string.Join(" ", rejected.Reasons)),
            _ => throw new InvalidOperationException("Preparation returned an unexpected outcome.")
        };
    }

    private static Result<TaskItem> CheckEligibility(TaskItem task, DateOnly date)
    {
        if (task.Completed)
            return Result<TaskItem>.Fail($"Task '{task.Id}' is already completed.");
        if (task.Date != date)
            return Result<TaskItem>.Fail($"Task '{task.Id}' belongs to {task.Date:yyyy-MM-dd}, not {date:yyyy-MM-dd}.");
        return Result<TaskItem>.Ok(task);
    }

    private static Result<PlanningState.Confirmed> ConfirmPrepared(PlanningState.Prepared prepared) =>
        PlanningWorkflow.Confirm(prepared) switch
        {
            PlanningTransition.Changed { Next: PlanningState.Confirmed confirmed } =>
                Result<PlanningState.Confirmed>.Ok(confirmed),
            PlanningTransition.Rejected rejected =>
                Result<PlanningState.Confirmed>.Fail("Confirmation rejected: " + string.Join(" ", rejected.Reasons)),
            _ => throw new InvalidOperationException("Confirmation returned an unexpected outcome.")
        };
}
