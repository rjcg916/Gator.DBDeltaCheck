using CommandLine;
using Microsoft.Extensions.Hosting;

namespace Gator.DBDeltaCheck.Core.Implementations.Commands
{
    public partial class CommandLineHandler
    {
        private readonly IHost _host;

        public CommandLineHandler(IHost host)
        {
            _host = host;
        }

        // This is the main entry point called by Program.cs
        public async Task<int> RunCommandAsync(string[] args)
        {
            // Use the parser to determine which command to run
            return await Parser.Default.ParseArguments<ScaffoldOptions, GenerateTemplateOptions>(args)
                .MapResult(
                    (ScaffoldOptions opts) => RunScaffolder(opts),
                    (GenerateTemplateOptions opts) => RunTemplateGenerator(opts),
                    errs => Task.FromResult(1)); // Return error code on parsing failure
        }
    }
}