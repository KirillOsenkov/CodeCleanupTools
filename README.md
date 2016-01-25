CodeCleanupTools
================
A set of command-line tools to cleanup C# and VB source code.

**WARNING: use these tools at your own risk**. The author and maintainers are not responsible for any damage caused to your files as a result of using these tools. Always manually code review changes made by the tools and always use a version control system.

A recommended pattern is that you fork this repo for a codebase and tweak to the guidelines of your codebase. It tries to use reasonable defaults (4 spaces, no tabs, no-BOM, CRLF, etc)

TextAndWhitespace
-----------------
Opens every *.cs file (or other pattern) in the current directory and all subdirectories and:
  1. Saves the file with UTF8 encoding without signature (no-BOM)
  2. Converts all line endings to CRLF (Windows)
  3. Removes trailing whitespace from every line
  4. Replaces leading tabs with 4 spaces
  5. Collapses multiple consecutive empty lines into one

SortProjectItems
----------------
Sorts the Compile and Reference items in ItemGroups of your *.csproj and *.vbproj files. This simplifies merging project files because the items are sorted alphabetically. It's easier to unify two different versions of a project if both are sorted before merge.

**Usage**:
  1. cinst sortprojectitems
  2. cd RootOfYourSolution
  3. sortprojectitems /r

It will recursively find all *.csproj and *.vbproj files in the current directory and all subdirectories and sort all MSBuild ItemGroups. It will also consolidate ItemGroups by kind and remove empty ItemGroups.

Note: Visual Studio normally tries to preserve sorting when modifying the project, however:
  * several operations such as Rename in Solution Explorer and Include in Project for resource files are known to break the order.
  * once the order is broken (an item is inserted in incorrect location), Visual Studio keeps making it worse, so subsequent modifications drift towards random ordering.
  * it helps to keep all branches sorted, so that even if projects are significantly diverged, there is a well-defined "normal form" for a project.

FormatSolution
--------------
Command line tool to load an .sln file and format every *.cs and *.vb file in the solution using the Visual Studio default formatting settings (uses Roslyn). Currently requires Visual Studio 2015 to be installed on the machine (for MSBuild 14). Saves every file back to disk.

Additionally the tool collapses multiple consecutive empty lines into a single empty line (a fix for StyleCop rule http://www.stylecop.com/docs/SA1507.html).

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
