using System.Diagnostics;
using System.Threading.Tasks;

using Serilog;

namespace PluralKit.Bot
{
    public class CpuStatService
    {
        private readonly ILogger _logger;
        
        public double LastCpuMeasure { get; private set; }

        public CpuStatService(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the current CPU usage. Estimation takes ~5 seconds (of mostly sleeping).
        /// </summary>
        public async Task<double> EstimateCpuUsage()
        {
            // We get the current processor time, wait 5 seconds, then compare
            // https://medium.com/@jackwild/getting-cpu-usage-in-net-core-7ef825831b8b
            
            _logger.Debug("Estimating CPU usage...");
            var stopwatch = new Stopwatch();
            
            stopwatch.Start();
            var cpuTimeBefore = Process.GetCurrentProcess().TotalProcessorTime;
            
            await Task.Delay(5000);
            
            stopwatch.Stop();
            var cpuTimeAfter = Process.GetCurrentProcess().TotalProcessorTime;

            var cpuTimePassed = cpuTimeAfter - cpuTimeBefore;
            var timePassed = stopwatch.Elapsed;

            var percent = cpuTimePassed / timePassed;
            _logger.Debug("CPU usage measured as {Percent:P}", percent);
            LastCpuMeasure = percent;
            return percent;
        }
    }
}