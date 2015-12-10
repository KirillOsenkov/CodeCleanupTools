using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public class RemoveConsecutiveEmptyLinesWorker
{
    public static Document Process(Document document)
    {
        var root = document.GetSyntaxRootAsync().Result;
        var newRoot = new Rewriter().Visit(root);
        if (newRoot != root)
        {
            document = document.WithSyntaxRoot(newRoot);
            FormatSolution.Write("Removing empty lines: " + document.FilePath);
        }

        return document;
    }

    private class Rewriter : CSharpSyntaxRewriter
    {
        public override bool VisitIntoStructuredTrivia
        {
            get { return true; }
        }

        public override SyntaxTriviaList VisitList(SyntaxTriviaList list)
        {
            list = base.VisitList(list);

            var lineBreaksAtBeginning = list.TakeWhile(t => t.IsKind(SyntaxKind.EndOfLineTrivia)).Count();
            if (lineBreaksAtBeginning > 1)
            {
                list = SyntaxFactory.TriviaList(list.Skip(lineBreaksAtBeginning - 1));
            }

            return list;
        }
    }
}