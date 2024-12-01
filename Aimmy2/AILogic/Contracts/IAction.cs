namespace Aimmy2.AILogic.Contracts;

public interface IAction : IDisposable
{
    public AIManager AIManager { get; set; }
    bool Active { get; }
    Task Execute(Prediction[] predictions);
    Task OnPause();
    Task OnResume();
}
