using ManagedXZ;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Tests.Integration.ManagedXZ
{
    public class ManagedXZTests
    {
        private const int CNT = 100;
        private static readonly Random rnd = new Random();

        [Test]
        public void CompressDecompressMemoryStreamTest()
        {
            string text = "12381293diushfidshf7sy982394239hdeuhd9932n3b0abh213bdsdc098h23ubcfuisbcv907h20uib0ub";

            using (var rawStream = new MemoryStream())
            {
                using (var xzStream = new XZCompressStream(rawStream, 1, 1, true))
                using (var inStream = new StreamWriter(xzStream))
                {
                    inStream.Write(text);
                }

                rawStream.Seek(0, SeekOrigin.Begin);
                byte[] data = rawStream.ToArray();
                var result = XZUtils.DecompressBytes(data, 0, data.Length);

                Assert.AreEqual(Encoding.UTF8.GetBytes(text), result);
            }
        }

        [Test]
        public void CompressDecompressFileStreamTest()
        {
            string text = "12381293diushfidshf7sy982394239hdeuhd9932n3b0abh213bdsdc098h23ubcfuisbcv907h20uib0ub";

            var fileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "test.txt.xz");
            var fileName1 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "test.txt");

            try
            {
                using (var rawStream = new FileStream(fileName, FileMode.Create))
                {
                    using (var xzStream = new XZCompressStream(rawStream))
                    using (var inStream = new StreamWriter(xzStream))
                    {
                        inStream.Write(text);
                    }

                    XZUtils.DecompressFile(fileName, fileName1);

                    string result;
                    using (var reader = new StreamReader(fileName1))
                    {
                        result = reader.ReadToEnd();
                    }

                    Assert.AreEqual(Encoding.UTF8.GetBytes(text), result);
                }
            }
            finally
            {
                if (File.Exists(fileName))
                    File.Delete(fileName);

                if (File.Exists(fileName1))
                    File.Delete(fileName1);
            }
        }


        [Test]
        public void CompressDecompressInMemoryTest()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 1000; i++)
                sb.AppendLine($"{i}: a random number {rnd.NextDouble()}");

            var str = sb.ToString();

            var bytes = Encoding.UTF8.GetBytes(str);
            var compressed = XZUtils.CompressBytes(bytes, 0, bytes.Length);
            var bytes2 = XZUtils.DecompressBytes(compressed, 0, compressed.Length);
            var str2 = Encoding.UTF8.GetString(bytes2);

            Assert.AreEqual(str, str2);
        }

        [TestCase(1)]
        public void CompressMultiStreamTest(int threads)
        {
            string text = "12381293diushfidshf7sy982394239hdeuhd9932n3b0abh213bdsdc098h23ubcfuisbcv907h20uib0ub";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(text);
            sb.AppendLine(text);
            sb.AppendLine(text);
            sb.AppendLine(text);
            sb.AppendLine(text);
            var res = sb.ToString();

            var fileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "test.xz");

            var timer = Stopwatch.StartNew();

            // create a new xz file
            using (var fs = File.Create(fileName))
            using (var xz = new XZCompressStream(fs, threads))
            using (var writer = new StreamWriter(xz, Encoding.UTF8))
            {
                writer.WriteLine(text);
                writer.WriteLine(text);
                writer.WriteLine(text);
            }

            // open the same xz file and append new data
            using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var xz = new XZCompressStream(fs, threads))
            using (var writer = new StreamWriter(xz, new UTF8Encoding(false, true))) // append data should go without BOM
            {
                writer.WriteLine(text);
                writer.WriteLine(text);
            }

            string result;

            using (var fileStream = new FileStream(fileName, FileMode.Open))
            using (var dec = new XZDecompressStream(fileStream))
            using (var reader = new StreamReader(dec))
            {
                result = reader.ReadToEnd();
            }

            Assert.AreEqual(res, result);
        }


        [TestCase("0byte.bin", "0byte.bin.xz", "compress 0byte")]
        [TestCase("1byte.0.bin", "1byte.0.bin.xz", "compress 1byte[0x00]")]
        [TestCase("1byte.1.bin", "1byte.1.bin.xz", "compress 1byte[0x01]")]
        public void TestCompressFile(string srcFileName, string xzFileName, string testName)
        {
            var tmpfile = Path.GetTempFileName();
            XZUtils.CompressFile(GetFile(srcFileName), tmpfile, FileMode.Append);
            var isSame = CompareFile(GetFile(xzFileName), tmpfile);
            File.Delete(tmpfile);

            Assert.IsTrue(isSame, testName);
        }

        [TestCase(new byte[0], "0byte.bin.xz", "compress 0byte in memory")]
        [TestCase(new byte[1] { 0 }, "1byte.0.bin.xz", "compress 1byte[0x00] in memory")]
        [TestCase(new byte[1] { 1 }, "1byte.1.bin.xz", "compress 1byte[0x00] in memory")]
        public void TestCompressInMemory(byte[] input, string xzFilename, string testName)
        {
            var data1 = XZUtils.CompressBytes(input, 0, input.Length);
            var data2 = File.ReadAllBytes(GetFile(xzFilename));
            Assert.AreEqual(data1, data2, testName);
        }

        [TestCase("0byte.bin.xz", "0byte.bin", "decompress 0byte")]
        [TestCase("1byte.0.bin.xz", "1byte.0.bin", "decompress 1byte[0x00]")]
        [TestCase("1byte.1.bin.xz", "1byte.1.bin", "decompress 1byte[0x01]")]
        public void TestDecompressFile(string xzFilename, string binFilename, string testName)
        {
            var tmpfile = Path.GetTempFileName();
            File.Delete(tmpfile);
            XZUtils.DecompressFile(GetFile(xzFilename), tmpfile);
            var isSame = CompareFile(GetFile(binFilename), tmpfile);
            File.Delete(tmpfile);

            Assert.IsTrue(isSame, testName);
        }

        [TestCase(new byte[0], 0, "0byte.bin", "decompress 0byte in memory")]
        [TestCase(new byte[1] { 0 }, 1, "1byte.0.bin", "decompress 1byte[0x00] in memory")]
        [TestCase(new byte[1] { 1 }, 1, "1byte.1.bin", "decompress 1byte[0x00] in memory")]
        public void TestDecompressInMemory(byte[] input, int count, string binFilename, string testName)
        {
            input = XZUtils.CompressBytes(input, 0, count);
            var data1 = XZUtils.DecompressBytes(input, 0, input.Length);
            var data2 = File.ReadAllBytes(GetFile(binFilename));

            Assert.AreEqual(data1, data2, testName);
        }

        private bool BytesEqual(byte[] arr1, byte[] arr2)
        {
            if (arr1.Length != arr2.Length) return false;
            for (int i = 0; i < arr1.Length; i++)
                if (arr1[i] != arr2[i])
                    return false;
            return true;
        }

        private bool TestCompressFile(string srcFilename, string xzFilename)
        {
            var tmpfile = Path.GetTempFileName();
            XZUtils.CompressFile(srcFilename, tmpfile, FileMode.Append);
            var isSame = CompareFile(xzFilename, tmpfile);
            File.Delete(tmpfile);
            return isSame;
        }

        private bool CompareFile(string file1, string file2)
        {
            var f1 = new FileInfo(file1);
            var f2 = new FileInfo(file2);
            if (f1.Length != f2.Length) return false;

            using (var fs1 = f1.OpenRead())
            using (var fs2 = f2.OpenRead())
            {
                const int SIZE = 1024 * 1024;
                var buffer1 = new byte[SIZE];
                var buffer2 = new byte[SIZE];
                while (true)
                {
                    var cnt = fs1.Read(buffer1, 0, SIZE);
                    fs2.Read(buffer2, 0, SIZE);
                    if (!BytesEqual(buffer1, buffer2)) return false;
                    if (cnt < SIZE) break;
                }

                return true;
            }
        }

        private string GetFile(string fileName)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", fileName);
        }

    }
}

