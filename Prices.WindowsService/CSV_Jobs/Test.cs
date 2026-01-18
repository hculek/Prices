using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prices.WindowsService.CSV_Jobs
{
    public class Test : Base<Test>
    {
        private readonly ILogger<Test> _logger;
        public Test(ILogger<Test> logger) : base(logger, "Test", 1, 1)
        {
            _logger = logger;
        }

        public override async Task Work()
        {
            throw new Exception("Test");
        }

    }
}
