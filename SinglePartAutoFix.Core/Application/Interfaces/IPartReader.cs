using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using System.Collections.Generic;

namespace SinglePartAutoFix.Application.Interfaces
{
    public interface IPartReader
    {
        IReadOnlyList<PartInfo> GetParts(PartQuery query);
    }
}
