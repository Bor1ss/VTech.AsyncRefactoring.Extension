using System.Threading.Tasks;

using VTech.AsyncRefactoring.Base;
using VTech.AsyncRefactoring.Base.MethodSelector;

namespace VTech.AsyncRefactoring.Console
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //var path = @"C:\Magistartura\Baigiamasis darbas\Code\TestSolution\TestSolution.sln";
            //var path = @"C:\Magistartura\Baigiamasis darbas\Code\VTech.Demo\VTech.Demo\VTech.Demo.sln";
            //var path = @"C:\Magistartura\Baigiamasis darbas\Code\TestSolution\TestSolution.sln";
            var path = @"C:\Magistartura\Baigiamasis darbas\Code\TestSolution2\TestSolution2.sln";
            //var path = @"C:\Magistartura\Baigiamasis darbas\Code\TestProject\TestProject.sln";
            //var path = @"C:\Workspaces\4teambiz\FT.Services\FT.Services.sln";
            //var path = @"C:\Workspaces\syncgene\CloudPlatform.sln";

            AsyncronizationProcessor asyncronizationProcessor = new(path);
            await asyncronizationProcessor.InitializeCodeMapAsync();
            var changes = asyncronizationProcessor.CollectSuggestedChanges(new AllMethodSelector());
            await asyncronizationProcessor.ApplyChangesAsync(changes);
            System.Console.ReadKey();
        }
    }
}
