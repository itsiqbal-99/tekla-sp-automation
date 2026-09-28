using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public interface IPartReader
    {
        IReadOnlyList<PartInfo> GetParts();
    }
}
