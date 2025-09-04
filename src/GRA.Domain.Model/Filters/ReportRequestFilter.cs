using System;
using System.Collections.Generic;
using System.Text;

namespace GRA.Domain.Model.Filters
{
    internal class ReportRequestFilter : BaseFilter
    {
        public int? ReportId { get; set; }
        public int? RequestedByUserId { get; set; }
    }
}
