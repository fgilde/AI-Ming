using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using InputLogic;
using Nextended.Core.Helper;

namespace Aimmy2.AILogic.Actions;

public abstract class BaseAction : IAction
{
    public AIManager AIManager { get; set; }
    public Task Execute(Prediction[] predictions) => Task.Run(() => ExecuteAsync(predictions.ToArray()));
    public virtual Task OnPause() => Task.CompletedTask;

    public virtual Task OnResume() => Task.CompletedTask;

    public abstract Task ExecuteAsync(Prediction[] predictions);

    public virtual bool Active => AppConfig.Current.ToggleState.GlobalActive;
    public IPredictionLogic PredictionLogic => AIManager.PredictionLogic;
    public ICapture ImageCapture => AIManager?.ImageCapture;

    public static IList<IAction> AllActions()
    {
        return typeof(BaseAction).Assembly.GetTypes()
            .Where(t => t.ImplementsInterface(typeof(IAction)) && !t.IsAbstract)
            .Select(t => (IAction)Activator.CreateInstance(t)).ToList();
    }

    protected bool HasValidKey(IEnumerable<StoredInputBinding> triggerKeys)
    {
        return triggerKeys.Any(k => k is { IsValid: true });
    }

    protected bool AnyKeyIsHold(IEnumerable<StoredInputBinding> triggerKeys)
    {
        return triggerKeys.Any(k => k is { IsValid: true } && KeyIsUnsetOrHold(k));
    }

    protected bool AllKeysAreUnsetOrHold(IEnumerable<StoredInputBinding> triggerKeys)
    {
        return triggerKeys.All(KeyIsUnsetOrHold);
    }

    protected bool KeyIsUnsetOrHold(StoredInputBinding triggerKey)
    {
        return !triggerKey.IsValid || InputBindingManager.IsHoldingBindingFor(triggerKey, TimeSpan.FromSeconds(triggerKey.MinTime));
    }

    protected bool AllKeysAreNotHold(IEnumerable<StoredInputBinding> triggerKeys)
    {
        return triggerKeys.All(KeyIsNotHold);
    }

    protected bool AnyKeysIsNotHold(IEnumerable<StoredInputBinding> triggerKeys)
    {
        return triggerKeys.Any(k => k is { IsValid: true } && KeyIsNotHold(k));
    }

    protected bool KeyIsNotHold(StoredInputBinding triggerKey)
    {
        return !triggerKey.IsValid || !InputBindingManager.IsHoldingBindingFor(triggerKey, TimeSpan.FromSeconds(triggerKey.MinTime));
    }

    public virtual void Dispose()
    {

    }
}