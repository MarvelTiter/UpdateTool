using AutoInjectGenerator;
using Microsoft.Extensions.Logging;

namespace UpdateTool.Core;

public delegate Task<StepActionDelegate<TContext>?> StepActionDelegate<TContext>(TContext context, CancellationToken cancellationToken);

[AutoInject]
public class StepRunner<TContext>(ILogger<StepRunner<TContext>> logger)
{
    private StepActionDelegate<TContext>? currentStep;
    private StepActionDelegate<TContext>? startStep;
    private bool enableStepWait = false;
    private TaskCompletionSource<bool>? pauseTcs;
    public void SetStart(StepActionDelegate<TContext> startStep)
    {
        this.startStep = startStep;
        this.currentStep = startStep;
    }

    public void SetStepWaitEnabled(bool enabled)
    {
        enableStepWait = enabled;
        if (!enabled)
        {
            Continue();
        }
    }

    private Task<StepActionDelegate<TContext>?>? currentTask;
    public async Task ExecuteAsync(TContext context, CancellationToken cancellationToken)
    {
        while (currentStep is not null)
        {
            if (enableStepWait)
            {
                logger.LogInformation("等待继续信号");
                await Wait();
            }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentTask = currentStep.Invoke(context, cancellationToken);
                var nextStep = await currentTask;

                if (nextStep is not null)
                {
                    if (enableStepWait)
                    {
                        logger.LogInformation("当前步骤执行完成");
                    }
                    currentStep = nextStep;
                }
                else
                {
                    if (enableStepWait)
                    {
                        logger.LogInformation("全部步骤执行完成");
                    }
                    currentStep = null;
                }
            }
            catch (OperationCanceledException)
            {
                currentStep = null;
                pauseTcs = null;
                break;
            }
            finally
            {
                currentTask = null;
            }
        }

    }
    private async Task Wait()
    {
        if (pauseTcs is null || pauseTcs is { Task.IsCompleted: true })
        {
            pauseTcs = new();
        }
        try
        {
            await pauseTcs.Task;
        }
        finally
        {
            pauseTcs = null;
        }
    }
    public void Continue()
    {
        if (currentTask is not null && !currentTask.IsCompleted)
        {
            return;
        }

        pauseTcs?.TrySetResult(true);
    }

    public void Reset()
    {
        if (pauseTcs is not null && !pauseTcs.Task.IsCompleted)
        {
            pauseTcs.TrySetCanceled();  // 通知等待者取消
            pauseTcs = null;
        }

        currentStep = startStep;

    }
}
