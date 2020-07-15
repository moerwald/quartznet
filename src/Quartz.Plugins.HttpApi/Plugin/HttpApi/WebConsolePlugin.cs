using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using Quartz.Spi;

namespace Quartz.Plugin.HttpApi
{
    public class HttpApiPlugin : ISchedulerPlugin
    {
        private IHost? host;

        public string HostName { get; set; } = "127.0.0.1";
        public int? Port { get; set; }

        public Task Initialize(string pluginName, IScheduler scheduler, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task Start(CancellationToken cancellationToken)
        {
            string baseAddress = $"http://{HostName ?? "localhost"}:{Port ?? 28682}/";

            //host = WebApp.Start<Startup>(url: baseAddress);
            host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
                .Build();
            
            // log.InfoFormat("Quartz Web Console bound to address {0}", baseAddress);
            return Task.CompletedTask;
        }

        public Task Shutdown(CancellationToken cancellationToken)
        {
            host?.Dispose();
            return Task.CompletedTask;
        }
    }
}