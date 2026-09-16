using Gaffer.Application.Run;
using Gaffer.Common;

namespace Gaffer.Presentation.Shell
{
    /// <summary>
    /// Where a run is written when the manager asks for it. Presentation may not see the save adapter —
    /// the file, the format and the path are Infrastructure's and Composition's business — so the shell
    /// is handed this one verb and nothing else. A failure is the adapter's own sentence (CONVENTIONS §4)
    /// and is shown where the manager pressed Save.
    /// </summary>
    public interface IRunPersistence
    {
        Result Save(RunSession session);
    }
}
