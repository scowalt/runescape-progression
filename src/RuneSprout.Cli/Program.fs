module RuneSprout.Cli.Program

let private help = """RuneSprout — standalone planner

Usage:
  RuneSprout.Cli --help

Planning is not implemented yet.
"""

[<EntryPoint>]
let main arguments =
    match arguments with
    | [| "--help" |] ->
        printf "%s" help
        0
    | _ ->
        eprintfn "Unrecognized arguments. Use --help for usage."
        2
