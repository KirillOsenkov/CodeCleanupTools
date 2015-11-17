using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class ExtensionMethods
{
    public static int IndexOfFirstNonSpaceCharacter(this string str)
    {
        bool found = false;
        int index;
        for (index = 0; index < str.Length; index++)
        {
            if (char.IsWhiteSpace(str[index]))
            {
                continue;
            }
            else
            {
                found = true; // this is nicer than using str.Length comparisons to index
                break;
            }
        }

        if (found) return index;
        else return -1;
    }

}
