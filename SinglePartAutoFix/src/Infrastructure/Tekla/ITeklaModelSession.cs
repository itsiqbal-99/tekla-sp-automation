using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    internal interface ITeklaModelSession
    {
        bool IsConnected();
        string GetModelName();
        string GetModelPath();

    }
}
