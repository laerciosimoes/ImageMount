using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImageMounter.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.Interop.Tests
{
    [TestClass()]
    public class PinnedStringTests
    {
        [TestMethod()]
        public void PinnedStringTest()
        {
            var testStr = "Test String";
            var pinnedString = new PinnedString(testStr);
            Assert.AreEqual(testStr, pinnedString.Target);
            Assert.AreEqual(22, pinnedString.UnicodeString.Length);
            Assert.AreEqual(testStr, pinnedString.UnicodeString.ToString());

        }

        [TestMethod()]
        public void PinnedStringEmptyTest()
        {
            var testStr = new string('\0', 10);
            var pinnedString = new PinnedString(10);
            Assert.AreEqual(testStr, pinnedString.Target);
            Assert.AreEqual(20, pinnedString.UnicodeString.Length);
            Assert.AreEqual(testStr, pinnedString.UnicodeString.ToString());

        }
    }
}