module Program

[<EntryPoint>]
let main _ = Harness.run (ModelTests.all @ ExportTests.allWithFixtures @ EditorAppTests.all @ EditorCharacterizationTests.all @ EditorEventContractTests.all @ EditorConformanceTests.all @ CollaborationTests.all @ WorkflowStudioTests.all @ WorkflowStudioTests.exportTests @ WorkflowStudioTests.interactiveTests @ WorkflowStudioTests.componentTests)
