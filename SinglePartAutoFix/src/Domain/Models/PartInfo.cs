using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Domain.Models
{
    public class PartInfo
    {
        public int Id { get; set; }
        public string PieceMark { get; set; }
        public string Profile { get; set; }
        public string Material { get; set; }
    }
}
