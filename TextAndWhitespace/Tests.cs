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
    public void TestTrimLeadingTabs()
    {
        var t1 = "this is one.\r\n\tthis is two.\r\n    \tthis is three\r\n\r\n\r\nthis is four.\r\n    \t    this is five.";
        var t2 = "this is one.\r\n    this is two.\r\n        this is three\r\n\r\n\r\nthis is four.\r\n            this is five.";

        var t3 = TextAndWhitespace.TrimLeadingTabsFromEveryLine(t1);

        Assert.AreEqual(t2, t3);
    }
}
