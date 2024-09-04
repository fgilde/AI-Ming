
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aimmy2.AILogic.Contracts;

public interface IAction : IDisposable
{
    public AIManager AIManager { get; set; }
    Task Execute(Prediction[] predictions);
    Task OnPause();
    Task OnResume();
}
