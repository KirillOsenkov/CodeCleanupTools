CodeCleanupTools
================
A set of command-line tools to cleanup C# and VB source code.

**WARNING: use these tools at your own risk**. The author and maintainers are not responsible for any damage caused to your files as a result of using these tools. Always manually code review changes made by the tools and always use a version control system.

TextAndWhitespace
-----------------
Opens every *.cs file (or other pattern) in the current directory and all subdirectories and:
  1. Saves the file with UTF8 encoding with signature (BOM)
  2. Converts all line endings to CRLF (Windows)
  3. Removes trailing whitespace from every line

SortProjectItems
----------------
Sorts the Compile and Reference items in ItemGroups of your *.csproj and *.vbproj files. This simplifies merging project files because the items are sorted alphabetically. It's easier to unify two different versions of a project if both are sorted before merge.

**Usage**:
  1. cinst sortprojectitems
  2. cd RootOfYourSolution
  3. sortprojectitems /r

It will recursively find all *.csproj and *.vbproj files in the current directory and all subdirectories and sort all MSBuild ItemGroups. It will also consolidate ItemGroups by kind and remove empty ItemGroups.

FormatSolution
--------------
Command line tool to load an .sln file and format every *.cs and *.vb file in the solution using the Visual Studio default formatting settings (uses Roslyn). Currently requires Visual Studio 2015 to be installed on the machine (for MSBuild 14). Saves every file back to disk.

**Usage**:
formatsolution <path-to-sln>.sln

MoveUsings
----------
Command line tool to move all using declarations in *.cs files from inside namespaces to top level. The tool currently only uses the syntactic part of Roslyn, i.e. it moves the using declarations and tries to preserve whitespace and comments, however it doesn't do any binding analysis, so be prepared to deal with build errors resulting from identifiers no longer binding or binding to a different symbol (you will especially get problems if you have a namespace and a type with the same name in any of your projects or references).

The reverse side (from top level to inside of namespaces) is not (yet) implemented.

LOC
---
Very simple tool to count the lines of code in all *.cs files in the current directory and all subdirectories.

**Usage**:
  1. cinst loc
  2. cd <your-solution-root>
  3. loc [*.ext]

It will print the number of lines of code. Use loc *.js to specify an optional mask (by default *.cs is assumed).

FindProjectsWithSameGuid
------------------------
Sometimes people copy-paste the .csproj file and leave the GUID of the original project. This confuses the VS project system. This tool finds projects that share the same project GUID if any. The tool has no output if it didn't find anything (the good case).
