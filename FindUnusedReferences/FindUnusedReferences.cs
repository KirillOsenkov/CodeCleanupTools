using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace RemoveUnusedReferences
{
    public class SolutionAnalyzer
    {
        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: RemoveUnusedReferences <path-to-solution.sln>");
                return;
            }

            string path = args[0];
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found: " + path);
                return;
            }

            path = Path.GetFullPath(path);
            Environment.CurrentDirectory = Path.GetDirectoryName(path);

            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();

            File.WriteAllText(reportFileName, "");
            new SolutionAnalyzer().Run(path);
        }

        private string[] assemblySearchPaths;

        private Dictionary<Guid, ProjectInfo> guidToProjectMap = new Dictionary<Guid, ProjectInfo>();
        private Dictionary<string, Assembly> assemblyNameToLoadedAssemblyMap = new Dictionary<string, Assembly>();
        private Dictionary<string, string> assemblyFileToAssemblyNameMap = new Dictionary<string, string>();
        private Dictionary<string, string> assemblyNameToAssemblyFileMap = new Dictionary<string, string>();

        private static readonly Guid csharpProjectTypeGuid = Guid.Parse("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
        private static readonly Guid visualBasicProjectTypeGuid = Guid.Parse("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}");
        private static readonly string reportFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Report.txt");

        private void Run(string path)
        {
            var solutionFilePath = Path.GetFullPath(path);
            var solutionFolder = Path.GetDirectoryName(solutionFilePath);
            var solution = SolutionFile.Parse(path);

            foreach (var projectBlock in solution.ProjectsInOrder)
            {
                var projectFullPath = projectBlock.AbsolutePath;
                var extension = Path.GetExtension(projectFullPath);
                if (!string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".vbproj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var projectGuid = Guid.Parse(projectBlock.ProjectGuid);
                var projectInfo = new ProjectInfo(
                    projectFullPath,
                    projectGuid,
                    projectBlock.ProjectName,
                    this);

                if (projectInfo.AssemblyName != null)
                {
                    guidToProjectMap.Add(projectGuid, projectInfo);
                }
                else
                {
                    Log("Could not find assembly for project: " + projectBlock.ProjectName);
                }
            }

            foreach (var kvp in guidToProjectMap)
            {
                AnalyzeProject(kvp.Value);
            }
        }

        private void AnalyzeProject(ProjectInfo projectInfo)
        {
            Log("Analyzing project: " + projectInfo.Name);
            var project = projectInfo.Project;

            SetAssemblySearchPathsFromProject(project);
            var projectReferences = GetProjectReferences(project);
            var metadataReferences = GetMetadataReferences(project);

            HashSet<string> transitiveClosure = new HashSet<string>();
            AddToClosure(transitiveClosure, projectInfo.References.Select(an => an.FullName));

            List<string> unusedProjectReferences = new List<string>();
            foreach (var projectReference in projectReferences)
            {
                if (!transitiveClosure.Contains(projectReference.AssemblyName.FullName))
                {
                    unusedProjectReferences.Add(projectReference.Name);
                }
            }

            List<string> unusedMetadataReferences = new List<string>();
            List<string> metadataReferencesThatCouldBeProjectReferences = new List<string>();
            foreach (var metadataReference in metadataReferences)
            {
                var assemblyName = GetAssemblyNameFromAssemblyFile(metadataReference);
                if (!transitiveClosure.Contains(assemblyName))
                {
                    unusedMetadataReferences.Add(assemblyName);
                }

                var candidateProject = guidToProjectMap.Values.Where(pi => pi.AssemblyName.FullName == assemblyName).FirstOrDefault();
                if (candidateProject != null)
                {
                    Log(string.Format("  MetadataReference {0} could be a project reference: {1}", metadataReference, candidateProject.Name));
                }
            }

            ReportUnusedProjectReferences(unusedProjectReferences);
            ReportUnusedMetadataReferences(unusedMetadataReferences);

            Log("=========");
        }

        private void ReportUnusedMetadataReferences(List<string> unusedMetadataReferences)
        {
            if (unusedMetadataReferences.Any())
            {
                Log("Unused metadata references:");
                foreach (var unusedMetadataReference in unusedMetadataReferences)
                {
                    Log("  Metadata: " + unusedMetadataReference);
                }
            }
        }

        private void ReportUnusedProjectReferences(List<string> unusedProjectReferences)
        {
            if (unusedProjectReferences.Any())
            {
                Log("Unused project references:");
                foreach (var unusedProjectReference in unusedProjectReferences)
                {
                    Log("  Project: " + unusedProjectReference);
                }
            }
        }

        private string[] GetMetadataReferences(Project project)
        {
            return (from reference in project.GetItems("Reference")
                    let path = GetAssemblyFileFromAssemblyName(
                        reference.EvaluatedInclude,
                        reference,
                        project)
                    where path != null
                    select path).ToArray();
        }

        private IEnumerable<ProjectInfo> GetProjectReferences(Project project)
        {
            return from reference in project.GetItems("ProjectReference")
                   let referenceGuid = Guid.Parse(reference.GetMetadataValue("Project"))
                   where guidToProjectMap.ContainsKey(referenceGuid)
                   select guidToProjectMap[referenceGuid];
        }

        private void SetAssemblySearchPathsFromProject(Project project)
        {
            assemblySearchPaths = project
                .GetPropertyValue("AssemblySearchPaths")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Concat(new[] { Path.Combine(project.GetPropertyValue("DevEnvDir"), "PrivateAssemblies") })
                .ToArray();
        }

        private string GetAssemblyNameFromAssemblyFile(string assemblyFile)
        {
            string result;
            if (assemblyFileToAssemblyNameMap.TryGetValue(assemblyFile, out result))
            {
                return result;
            }

            result = AssemblyName.GetAssemblyName(assemblyFile).FullName;
            assemblyFileToAssemblyNameMap.Add(assemblyFile, result);
            return result;
        }

        private void AddToClosure(HashSet<string> transitiveClosure, IEnumerable<string> references)
        {
            foreach (var reference in references)
            {
                AddToClosure(transitiveClosure, reference);
            }
        }

        private void AddToClosure(HashSet<string> transitiveClosure, string reference)
        {
            if (transitiveClosure.Contains(reference))
            {
                return;
            }

            transitiveClosure.Add(reference);
            Assembly assembly = GetAssemblyFromAssemblyName(reference);
            if (assembly == null)
            {
                return;
            }

            var references = assembly.GetReferencedAssemblies().Select(an => an.FullName);
            AddToClosure(transitiveClosure, references);
        }

        private string GetAssemblyFileFromAssemblyName(string assemblyName, ProjectItem reference = null, Project project = null)
        {
            string result;
            if (assemblyNameToAssemblyFileMap.TryGetValue(assemblyName, out result))
            {
                return result;
            }

            IDictionary metadata = null;
            if (reference != null)
            {
                metadata = reference.Metadata.ToDictionary(m => m.Name, m => m.EvaluatedValue);
            }

            ResolveAssemblyReference resolveTask = new ResolveAssemblyReference();
            string referenceString = assemblyName;
            TaskItem taskItem = metadata != null ? new TaskItem(referenceString, metadata) : new TaskItem(referenceString);
            resolveTask.Assemblies = new ITaskItem[] { taskItem };
            resolveTask.Silent = true;
            resolveTask.SearchPaths = assemblySearchPaths;

            if (project != null)
            {
                resolveTask.TargetFrameworkVersion = project.GetPropertyValue("TargetFrameworkVersion");
                resolveTask.TargetFrameworkMoniker = project.GetPropertyValue("TargetFrameworkMoniker");
                resolveTask.TargetFrameworkMonikerDisplayName = project.GetPropertyValue("TargetFrameworkMonikerDisplayName");
                resolveTask.TargetedRuntimeVersion = project.GetPropertyValue("TargetRuntimeVersion");
                resolveTask.TargetFrameworkDirectories = new[] { project.GetPropertyValue("FrameworkPathOverride") };
            }

            try
            {
                if (!resolveTask.Execute() || resolveTask.ResolvedFiles.Length == 0)
                {
                    Log("===> Failed to resolve assembly: " + assemblyName);
                    return null;
                }
            }
            catch
            {
                return null;
            }

            result = resolveTask.ResolvedFiles[0].ItemSpec;
            result = Path.GetFullPath(result);
            assemblyNameToAssemblyFileMap.Add(assemblyName, result);

            return result;
        }

        public Assembly GetAssemblyFromAssemblyName(string assemblyName)
        {
            Assembly result;
            if (assemblyNameToLoadedAssemblyMap.TryGetValue(assemblyName, out result))
            {
                return result;
            }

            if (File.Exists(assemblyName))
            {
                result = Assembly.ReflectionOnlyLoadFrom(assemblyName);
            }
            else
            {
                try
                {
                    result = Assembly.ReflectionOnlyLoad(assemblyName);
                }
                catch (Exception)
                {
                    try
                    {
                        string assemblyPath = GetAssemblyFileFromAssemblyName(assemblyName);
                        result = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            assemblyNameToLoadedAssemblyMap.Add(assemblyName, result);

            return result;
        }

        private void Log(string message)
        {
            Console.WriteLine(message);
            File.AppendAllText(reportFileName, message + Environment.NewLine);
        }

        public static AssemblyName[] GetReferences(string filePath)
        {
            using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(filePath))
            {
                var references = assemblyDefinition.MainModule.AssemblyReferences.Select(r => new AssemblyName(r.FullName)).ToArray();
                return references;
            }
        }
    }

    public class ProjectInfo
    {
        private string projectFullPath;

        public ProjectInfo(string projectFullPath, Guid guid, string projectName, SolutionAnalyzer solutionAnalyzer)
        {
            this.projectFullPath = projectFullPath;
            Project = new Project(projectFullPath);
            Guid = guid;

            TargetPath = Project.GetPropertyValue("TargetPath");
            TargetPath = Path.GetFullPath(TargetPath);
            if (File.Exists(TargetPath))
            {
                AssemblyName = AssemblyName.GetAssemblyName(TargetPath);

                References = SolutionAnalyzer.GetReferences(TargetPath);
            }

            Name = projectName;
        }

        public Project Project { get; set; }
        public Guid Guid { get; set; }
        public AssemblyName AssemblyName { get; set; }
        public string TargetPath { get; set; }
        public AssemblyName[] References { get; set; }
        public string Name { get; set; }
    }
}
