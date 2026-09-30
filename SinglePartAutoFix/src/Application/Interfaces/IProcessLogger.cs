using SinglePartAutoFix.Application.Models;
using System.Collections.Generic;

namespace SinglePartAutoFix.Application.Interfaces
{
    public interface IProcessLogger
    {
        void Write(string processName, IReadOnlyList<DrawingProcessResult> results);
    }
}
