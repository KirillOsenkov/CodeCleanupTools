using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class MoveUsings
{
    static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            PrintHelp();
            return 0;
        }

        var files = Directory.GetFiles(Environment.CurrentDirectory, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            MoveUsingsToTopOfTheFile(file);
        }

        return 0;
    }

    public static void MoveUsingsToTopOfTheFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        text = MoveUsingsToTop(text);
        File.WriteAllText(filePath, text, Encoding.UTF8);
    }

    public static string MoveUsingsToTop(string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new UsingsRemover();
        var hasTopLevelUsings = root.Usings.Any();
        root = (CompilationUnitSyntax)root.Accept(rewriter);
        var usings = rewriter.Usings.Select(u => StripLeadingWhitespace(u)).ToArray();
        if (usings.Length == 0)
        {
            return text;
        }

        if (!hasTopLevelUsings)
        {
            root = AddLineBreakBefore(root);
        }

        root = root.AddUsings(usings);

        text = root.ToFullString();
        return text;
    }

    private static T AddLineBreakBefore<T>(T node)
        where T : SyntaxNode
    {
        var token = node.GetFirstToken();
        var trivia = token.LeadingTrivia;
        trivia = trivia.Insert(0, SyntaxFactory.EndOfLine(Environment.NewLine));
        var newToken = token.WithLeadingTrivia(trivia);
        node = node.ReplaceToken(token, newToken);
        return node;
    }

    private static UsingDirectiveSyntax StripLeadingWhitespace(UsingDirectiveSyntax usingDirective)
    {
        var keyword = usingDirective.UsingKeyword;
        keyword = StripLeadingWhitespace(keyword);
        usingDirective = usingDirective.WithUsingKeyword(keyword);

        return usingDirective;
    }

    private static SyntaxToken StripLeadingWhitespace(SyntaxToken usingKeyword)
    {
        var triviaList = usingKeyword.LeadingTrivia;
        int lastWhitespace = FindLastWhitespacePosition(triviaList);

        while (lastWhitespace != -1)
        {
            triviaList = triviaList.RemoveAt(lastWhitespace);
            lastWhitespace = FindLastWhitespacePosition(triviaList);
        }

        usingKeyword = usingKeyword.WithLeadingTrivia(triviaList);
        return usingKeyword;
    }

    private static int FindFirstLineBreakPosition(SyntaxTriviaList triviaList)
    {
        for (int i = 0; i < triviaList.Count - 1; i++)
        {
            if (triviaList[i].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindLastWhitespacePosition(SyntaxTriviaList triviaList)
    {
        for (int i = triviaList.Count - 1; i >= 0; i--)
        {
            if (triviaList[i].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return i;
            }
        }

        return -1;
    }

    private class UsingsRemover : CSharpSyntaxRewriter
    {
        public List<UsingDirectiveSyntax> Usings = new List<UsingDirectiveSyntax>();

        public override SyntaxNode Visit(SyntaxNode node)
        {
            if (node == null)
            {
                return null;
            }

            var parentNamespace = node.Parent as NamespaceDeclarationSyntax;
            if (parentNamespace == null)
            {
                return base.Visit(node);
            }

            var children = parentNamespace.ChildNodes().ToArray();
            for (int i = 1; i < children.Length; i++)
            {
                if (children[i] == node)
                {
                    if (children[i - 1] is UsingDirectiveSyntax)
                    {
                        var token = node.GetFirstToken();
                        var trivia = token.LeadingTrivia;
                        var lineBreakIndex = FindFirstLineBreakPosition(trivia);
                        if (lineBreakIndex != -1)
                        {
                            return StripLeadingLineBreak(node, token, trivia, lineBreakIndex);
                        }
                    }

                    break;
                }
            }

            return base.Visit(node);
        }

        private SyntaxNode StripLeadingLineBreak(
            SyntaxNode node,
            SyntaxToken token,
            SyntaxTriviaList trivia,
            int triviaPosition)
        {
            node = base.Visit(node);
            if (node == null)
            {
                return null;
            }

            trivia = trivia.RemoveAt(triviaPosition);
            var newToken = token.WithLeadingTrivia(trivia);
            node = node.ReplaceToken(token, newToken);
            return node;
        }

        public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
        {
            Usings.Add(node);
            return null;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"Usage: MoveUsings.exe
       Moves using declarations in each .cs file in current directory and all subdirectories to top of file.");
    }
}
