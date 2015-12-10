using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class Tests
{
    private void GetLines(string text, params string[] expectedLines)
    {
        var actualLines = TextAndWhitespace.GetLines(text);
        Assert.AreEqual(expectedLines.Length, actualLines.Length);

        for (int i = 0; i < expectedLines.Length; i++)
        {
            Assert.AreEqual(expectedLines[i], actualLines[i]);
        }
    }

    [TestMethod]
    public void TestGetLines()
    {
        GetLines(
@"ab

c


d",
  "ab", "", "c", "", "", "d");
        GetLines("");
        GetLines("\r\n", "", "");
        GetLines("\n\r", "", "", "");
        GetLines("\r", "", "");
        GetLines("\n", "", "");
        GetLines(" \r\n", " ", "");
        GetLines("\r\n ", "", " ");
        GetLines("\r\n\r\n", "", "", "");
    }

    [TestMethod]
    public void TestReplaceLeadingTabsWithSpaces()
    {
        TabsToSpaces(
"\ta",
"    a");
        TabsToSpaces(
" a",
" a");
        TabsToSpaces(
" a\t",
" a\t");
        TabsToSpaces(
" a\t \t",
" a\t \t");
        TabsToSpaces(
"\t \ta",
"         a");
        TabsToSpaces(
"this is one.\r\n\tthis is two.\r\n    \tthis is three\r\n\r\n\r\nthis is four.\r\n    \t    this is five.",
"this is one.\r\n    this is two.\r\n        this is three\r\n\r\n\r\nthis is four.\r\n            this is five.");
    }

    private void TabsToSpaces(string original, string expected)
    {
        var actual = TextAndWhitespace.ReplaceLeadingTabsWithSpaces(original, 4);
        Assert.AreEqual(expected, actual);
    }
}
