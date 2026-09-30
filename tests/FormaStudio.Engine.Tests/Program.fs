module Program

[<EntryPoint>]
let main _ = Harness.run (ModelTests.all @ ExportTests.allWithFixtures @ EditorAppTests.all)
