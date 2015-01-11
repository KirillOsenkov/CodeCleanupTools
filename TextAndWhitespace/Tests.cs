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
}
