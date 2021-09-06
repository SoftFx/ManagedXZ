using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ManagedXZ
{
    internal static class Native
    {
        public static bool Is64Bit;

        static Native()
        {
            // check 32bit or 64bit
            PortableExecutableKinds peKinds;
            ImageFileMachine arch;
            typeof(object).Module.GetPEKind(out peKinds, out arch);
            string resourceName;
            if (arch == ImageFileMachine.AMD64)
            {
                resourceName = "ManagedXZ.build.liblzma_amd64.dll";
                Is64Bit = true;
            }
            else if (arch == ImageFileMachine.I386)
            {
                resourceName = "ManagedXZ.build.liblzma_x86.dll";
                Is64Bit = false;
            }
            else
                throw new Exception(arch + " is not supported yet");

            var assembly = Assembly.GetExecutingAssembly();
            var path = Path.Combine(Path.GetDirectoryName(assembly.Location), "liblzma.dll");
            byte[] contents;
            using (var input = assembly.GetManifestResourceStream(resourceName))
            {
                contents = new byte[input.Length];
                input.Read(contents, 0, (int)input.Length);
            }

            if (!File.Exists(path) || !BuffersEqual(File.ReadAllBytes(path), contents))
            {
                File.WriteAllBytes(path, contents);
            }
            var h = LoadLibrary(path);
            if (h == IntPtr.Zero)
                throw new ApplicationException("Cannot load liblzma.dll");
        }

        static bool BuffersEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; ++i)
                if (left[i] != right[i])
                    return false;
            return true;
        }

        public static void CheckSize()
        {
            Console.WriteLine($"sizeof(lzma_stream)={Marshal.SizeOf(typeof(lzma_stream))}");
            Console.WriteLine($"sizeof(lzma_mt)={Marshal.SizeOf(typeof(lzma_mt))}");
        }

        #region dll helpers

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, BestFitMapping = false, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr moduleHandle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr moduleHandle, string procname);

        #endregion

        #region xz functions

        [DllImport("liblzma.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern lzma_ret lzma_code(lzma_stream strm, lzma_action action);

        [DllImport("liblzma.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void lzma_end(lzma_stream strm);

        [DllImport("liblzma.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void lzma_get_progress(lzma_stream strm, out UInt64 progress_in, out UInt64 progress_out);

        [DllImport("liblzma.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern lzma_ret lzma_easy_encoder(lzma_stream strm, UInt32 preset, lzma_check check);

        [DllImport("liblzma.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern lzma_ret lzma_stream_encoder_mt(lzma_stream strm, lzma_mt options);

        internal const UInt32 LZMA_TELL_NO_CHECK = 0x01;
        internal const UInt32 LZMA_TELL_UNSUPPORTED_CHECK = 0x02;
        internal const UInt32 LZMA_TELL_ANY_CHECK = 0x04;
        internal const UInt32 LZMA_IGNORE_CHECK = 0x10;
        internal const UInt32 LZMA_CONCATENATED = 0x08;

        [DllImport("liblzma.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern lzma_ret lzma_auto_decoder(lzma_stream strm, UInt64 memlimit, UInt32 flags);

        #endregion
    }
}