using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;

class FixerLoader
{
    public static ImmutableArray<CodeFixProvider> LoadFixers(IEnumerable<Assembly> assemblies, string language)
    {
        return assemblies
            .SelectMany(GetConcreteTypes)
            .Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
            .Where(t => IsExportedForLanguage(t, language))
            .Select(CreateInstanceOfCodeFix)
            .OfType<CodeFixProvider>()
            .ToImmutableArray();
    }

    private static bool IsExportedForLanguage(Type codeFixProvider, string language)
    {
        var exportAttribute = codeFixProvider.GetCustomAttribute<ExportCodeFixProviderAttribute>(inherit: false);
        return exportAttribute is not null && exportAttribute.Languages.Contains(language);
    }

    private static CodeFixProvider CreateInstanceOfCodeFix(Type codeFixProvider)
    {
        try
        {
            return (CodeFixProvider)Activator.CreateInstance(codeFixProvider);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<Type> GetConcreteTypes(Assembly assembly)
    {
        try
        {
            var concreteTypes = assembly
                .GetTypes()
                .Where(type => !type.GetTypeInfo().IsInterface
                    && !type.GetTypeInfo().IsAbstract
                    && !type.GetTypeInfo().ContainsGenericParameters);

            // Realize the collection to ensure exceptions are caught
            return concreteTypes.ToList();
        }
        catch
        {
            return Type.EmptyTypes;
        }
    }
}