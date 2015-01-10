CodeCleanupTools
================
A set of command-line tools to cleanup C# and VB source code.

**WARNING: use these tools at your own risk**. The author and maintainers are not responsible for any damage caused to your files as a result of using these tools. Always manually code review changes made by the tools and always use a version control system.

SortProjectItems
----------------
Sorts the Compile and Reference items in ItemGroups of your *.csproj and *.vbproj files. This simplifies merging project files if the items are sorted alphabetically.

**Usage**:
  1. cinst sortprojectitems
  2. cd RootOfYourSolution
  3. sortprojectitems /r

It will recursively find all *.csproj and *.vbproj files in the current directory and all subdirectories and sort all MSBuild ItemGroups. It will also consolidate ItemGroups by kind and remove empty ItemGroups.

FindProjectsWithSameGuid
------------------------
Sometimes people copy-paste the .csproj file and leave the GUID of the original project. This confuses the VS project system. This tool finds projects that share the same project GUID if any. The tool has no output if it didn't find anything (the good case).
