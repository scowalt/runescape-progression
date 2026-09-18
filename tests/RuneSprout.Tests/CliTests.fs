module RuneSprout.Tests.CliTests

open System
open System.Diagnostics
open System.IO
open Xunit

let private runCli arguments =
    let host =
        match Environment.GetEnvironmentVariable "DOTNET_HOST_PATH" with
        | null | "" -> "dotnet"
        | path -> path

    let startInfo = ProcessStartInfo(host)
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "RuneSprout.Cli.dll"))

    for argument in arguments do
        startInfo.ArgumentList.Add argument

    use child = Process.Start startInfo
    let stdout = child.StandardOutput.ReadToEndAsync()
    let stderr = child.StandardError.ReadToEndAsync()

    if not (child.WaitForExit 10000) then
        child.Kill true
        child.WaitForExit()
        failwith "CLI did not exit within 10 seconds."

    child.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult()

[<Fact>]
let ``help explains the available CLI without claiming to plan`` () =
    let exitCode, stdout, stderr = runCli [ "--help" ]

    Assert.Equal(0, exitCode)
    Assert.Equal("", stderr)
    Assert.Contains("RuneSprout", stdout)
    Assert.Contains("Usage:", stdout)
    Assert.Contains("--help", stdout)
    Assert.Contains("Planning is not implemented yet.", stdout)

[<Theory>]
[<InlineData("plan")>]
[<InlineData("--unknown")>]
let ``unsupported commands fail rather than claim planning success`` argument =
    let exitCode, stdout, stderr = runCli [ argument ]

    Assert.Equal(2, exitCode)
    Assert.Equal("", stdout)
    Assert.Contains("--help", stderr)
