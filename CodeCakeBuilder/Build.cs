using Cake.Npm;
using Cake.Npm.RunScript;
using Cake.Common.IO;
using Cake.Common.Solution;
using Cake.Core;
using Cake.Core.Diagnostics;
using SimpleGitVersion;
using System.Linq;

namespace CodeCake
{
    /// <summary>
    /// Standard build "script".
    /// </summary>
    [AddPath( "%UserProfile%/.nuget/packages/**/tools*" )]
    public partial class Build : CodeCakeHost
    {
        public Build()
        {
            Cake.Log.Verbosity = Verbosity.Diagnostic;
            StandardGlobalInfo globalInfo = CreateStandardGlobalInfo()
                                                .AddDotnet()
                                                .AddNPM()
                                                .SetCIBuildTag();
            Task( "Check-Repository" )
                .Does( () =>
                {
                    globalInfo.TerminateIfShouldStop();
                } );

            Task( "Clean" )
                .IsDependentOn( "Check-Repository" )
                .Does( () =>
                {
                    globalInfo.GetDotnetSolution().Clean();
                    globalInfo.GetNPMSolution().Clean();
                } );


            Task( "Build" )
                .IsDependentOn( "Check-Repository" )
                .IsDependentOn( "Clean" )
                .Does( () =>
                {
                    globalInfo.GetDotnetSolution().Build();
                    globalInfo.GetNPMSolution().Build();
                } );

            Task( "Unit-Testing" )
                .IsDependentOn( "Build" )
                .WithCriteria( () => Cake.InteractiveMode() == InteractiveMode.NoInteraction
                                     || Cake.ReadInteractiveOption( "RunUnitTests", "Run Unit Tests?", 'Y', 'N' ) == 'Y' )
               .Does( () =>
               {
                   var testProjects = globalInfo.GetDotnetSolution().Projects.Where( p => p.Name.EndsWith( ".Tests" )
                                                                                          && !p.Path.Segments.Contains( "Integration" ) );
                   globalInfo.GetDotnetSolution().Test( testProjects );
                   globalInfo.GetNPMSolution().Test();
               } );

            Task( "Create-Packages" )
                .WithCriteria( () => globalInfo.IsValid )
                .IsDependentOn( "Unit-Testing" )
                .Does( () =>
                {
                    globalInfo.GetDotnetSolution().Pack();
                    globalInfo.GetNPMSolution().Pack();
                } );

            Task( "Push-Packages" )
                .WithCriteria( () => globalInfo.IsValid )
                .IsDependentOn( "Create-Packages" )
                .Does( () =>
                {
                    globalInfo.PushArtifacts();
                } );

            // The Default task for this script can be set here.
            Task( "Default" )
                .IsDependentOn( "Push-Packages" );

        }


    }
}
