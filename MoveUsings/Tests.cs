using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class Tests
{
    private void Test(string original, string expected)
    {
        var actual = MoveUsings.MoveUsingsToTop(original);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void BasicTest()
    {
        Test(

original:
@"namespace N
{
    using System;
    using System.Collections.Generic;

    class C
    {
    }
}
",

expected:
@"using System;
using System.Collections.Generic;

namespace N
{
    class C
    {
    }
}
");
    }

    [TestMethod]
    public void Comments()
    {
        Test(

original:
@"namespace N
{
    using System; // preserve

    using System.Collections.Generic;
}
",

expected:
@"using System; // preserve

using System.Collections.Generic;

namespace N
{
}
");
    }

    [TestMethod]
    public void Comments2()
    {
        Test(

original:
@"namespace N
{
    // usings
    using System; // preserve

    using System.Collections.Generic;
}
",

expected:
@"// usings
using System; // preserve

using System.Collections.Generic;

namespace N
{
}
");
    }

    [TestMethod]
    public void Whitespace()
    {
        Test(

original:
@"namespace N
{

    using System; // preserve

    using System.Collections.Generic;

}
",

expected:
@"
using System; // preserve

using System.Collections.Generic;

namespace N
{

}
");
    }

    [TestMethod]
    public void MultipleNamespaces()
    {
        Test(

original:
@"namespace N
{
    using System; // preserve
    using System.Collections.Generic;
}

namespace N2.N3
{
    using System.Linq;

    class C
    {
    }
}
",

expected:
@"using System; // preserve
using System.Collections.Generic;
using System.Linq;

namespace N
{
}

namespace N2.N3
{
    class C
    {
    }
}
");
    }

    [TestMethod]
    public void TopLevelNoop()
    {
        Test(

original:
@"using System;

namespace N { }
",

expected:
@"using System;

namespace N { }
");
    }

    [TestMethod]
    public void NoopWithNoUsings()
    {
        Test(

original:
@"namespace N { }
",

expected:
@"namespace N { }
");
    }
}
