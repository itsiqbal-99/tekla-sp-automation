using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Application.Models
{
    public class DrawingProcessResult
    {
        public DrawingCandidate Candidate { get; set; }
        public DrawingProcessStatus Status { get; set; }
        public String Message { get; set; }
    }
}
