using System.Threading.Tasks;

using ConsoleAppFramework;

using Microsoft.Extensions.Hosting;

namespace DiVVe.API
{
    public static class Program
    {
        public static async Task Main(string[] args)
            => await Host.CreateDefaultBuilder(args).RunConsoleAppFrameworkWebHostingAsync("http://localhost:58000");
    }
}
