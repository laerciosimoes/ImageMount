using ImageMounter.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageMounter.InteropTests
{
    [TestClass()]
    public class NativeCallsTests
    {
        [TestMethod()]
        public void GenRandomSByteTest()
        {
            var x = NativeCalls.GenRandomSByte();
            Assert.IsInstanceOfType(x, typeof(sbyte));
        }

        [TestMethod()]
        public void GenRandomInt16Test()
        {
            var x = NativeCalls.GenRandomInt16();
            Assert.IsInstanceOfType(x, typeof(short));

        }

        [TestMethod()]
        public void GenRandomInt32Test()
        {
            var x = NativeCalls.GenRandomInt32();
            Assert.IsInstanceOfType(x, typeof(int));

        }

        [TestMethod()]
        public void GenRandomInt64Test()
        {
            var x = NativeCalls.GenRandomInt64();
            Assert.IsInstanceOfType(x, typeof(long));

        }
        [TestMethod()]

        public void GenRandomByteTest()
        {
            var x = NativeCalls.GenRandomByte();
            Assert.IsInstanceOfType(x, typeof(byte));

        }
        [TestMethod()]

        public void GenRandomUInt16Test()
        {
            var x = NativeCalls.GenRandomUInt16();
            Assert.IsInstanceOfType(x, typeof(ushort));

        }
        [TestMethod()]

        public void GenRandomUInt32Test()
        {
            var x = NativeCalls.GenRandomUInt32();
            Assert.IsInstanceOfType(x, typeof(uint));

        }
        [TestMethod()]

        public void GenRandomUInt64Test()
        {
            var x = NativeCalls.GenRandomUInt64();
            Assert.IsInstanceOfType(x, typeof(ulong));

        }
        [TestMethod()]

        public void GenRandomGuidTest()
        {
            var x = NativeCalls.GenRandomGuid();
            Assert.IsInstanceOfType(x, typeof(Guid));

        }
        [TestMethod()]

        public void GenRandomBytesTest()
        {
            var x = NativeCalls.GenRandomBytes(10);
            Assert.IsInstanceOfType(x, typeof(byte[]));

        }
        [TestMethod()]

        public void GenerateDiskSignatureTest()
        {
            var x = NativeCalls.GenerateDiskSignature();
            Assert.IsInstanceOfType(x, typeof(uint));

        }

    }
}