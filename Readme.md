# What is VTech.AsyncRefactoring?

Tool to detect blocking constructions in .Net(C#) application and convert them to asynchronous version.

Blocking code descriptions can be found [here](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md)

The tool was developed to conduct an experiment to complete master thesis on "Research on Refactoring Methods for Ensuring Async Code in .NET Applications".

The tool can be utilized as [extension](https://marketplace.visualstudio.com/items?itemName=BorisK.AsyncRefactoring) to Visual Studio.

## Limitations
Currently, only methods and local functions are processed.


## Instruction to VS Extension

In order to use the VS extension follow the next steps:
1. Open context menu on any .cs file and select "Find and Fix async issues" 
![Context menu](https://github.com/Bor1ss/VTech.AsyncRefactoring.Extension/blob/main/resources/instruction_start_type_selection.png?raw=true)
2. The next step is to select the start point, which will be used to create a processable code graph
![Start method selection](https://github.com/Bor1ss/VTech.AsyncRefactoring.Extension/blob/main/resources/instructions_context_menu.png?raw=true)
3. Press "Apply" and wait utill dialog with suggested changes appear. In case of huge solution it may take few minutes
![Changes preview dialog](https://github.com/Bor1ss/VTech.AsyncRefactoring.Extension/blob/main/resources/instruction_changes_preview.png?raw=true)
4. Select changes to be applied. In case change is not required checkbox can be unmarked. If required, suggested change can be edited on dialog directly
5. Press "Apply" and changes will be saved to file system




