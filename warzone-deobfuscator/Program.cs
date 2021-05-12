using System;
using System.IO;

namespace warfare_dump_fixer
{
    class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length != 1)
                    throw new Exception("1> Incorrect usage, please drag & drop the modern warfare dump on this executable");

                string inputFilePath = args[0];
                string outputFilePath = Path.Combine(Path.GetDirectoryName(inputFilePath), Path.GetFileNameWithoutExtension(inputFilePath) + "_deobf.exe");
                Console.WriteLine("1> Input file: {0}", inputFilePath);
                Console.WriteLine("1> This process might takes several seconds");

                if (!File.Exists(inputFilePath))
                    throw new Exception("Input file not found");
                
                byte[] dataBuffer = File.ReadAllBytes(inputFilePath);
                if (dataBuffer.Length <= 0x1000)
                    throw new Exception("Invalid file length (less than 0x1000 bytes found)");

                for (ulong i = 0x600; i < (ulong)dataBuffer.Length; i++)
                {
                    JumpBack(ref dataBuffer, ref i);
                    JumpNear(ref dataBuffer, ref i);
                }
                
                File.WriteAllBytes(outputFilePath, dataBuffer);
                Console.WriteLine("1> Output file: {0}", outputFilePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("1> {0}", e.ToString());
            }

            Console.WriteLine("1> Press any key to continue...");
            Console.Read();
        }

        private static bool IsPush(byte value) => value >= 0x50 && value <= 0x57;

        private static bool IsPushfq(byte value) => value == 0x9C;

        private static bool IsPop(byte value) => value >= 0x58 && value <= 0x5F;

        private static bool IsPopfq(byte value) => value == 0x9D;

        private static void JumpNear(ref byte[] data, ref ulong i)
        {
            if (!(data[i] == 0x74 && data[i + 1] == 0x40))
                return;

            if (data[i + 0x42] != 0xE9)
                return;

            NopRange(ref data, i + 0x2, i + 0x41);
        }

        private static void JumpBack(ref byte[] data, ref ulong i)
        {
            if (!IsRelevantCall(ref data, i))
                return;

            var jumpPos = i - (0xFFUL - data[i + 1] - 0x4UL);
            if (data[jumpPos] != 0xEB)
                return;

            ulong nopStart = 0;
            var cursor = jumpPos;
            while (nopStart == 0)
            {
                if (!SearchBack(ref data, cursor, IsPushfq, out var pushfq))
                    throw new Exception("Unable to find the expected pushfq");

                if (IsPush(data[pushfq + 1]))
                    nopStart = pushfq;
                else if (IsPush(data[pushfq - 1]))
                    nopStart = pushfq - 1;

                cursor = pushfq;
            }

            var jumpDest = data[jumpPos + 1] + jumpPos + 0x2;

            ulong nopEnd = 0;
            cursor = jumpDest;
            while (nopEnd == 0)
            {
                if (!SearchForward(ref data, cursor, IsPopfq, out var popfq))
                    throw new Exception("Unable to find the expected popfq");

                if (IsPop(data[popfq + 1]))
                    nopEnd = popfq + 1;
                else if (IsPop(data[popfq - 1]))
                    nopEnd = popfq;

                cursor = popfq;
            }

            NopRange(ref data, nopStart, nopEnd);
        }

        private static void NopRange(ref byte[] data, ulong from, ulong to)
        {
            for (var i = from; i <= to; i++)
                data[i] = 0x90;
        }

        private static bool IsRelevantCall(ref byte[] data, ulong i)
        {
            if (data[i] != 0xE8)
                return false;

            if (i + 1 >= (ulong)data.Length) return false;

            if (data[i + 1] == 0xFF || data[i + 1] >> 4 != 0xF)
                return false;

            if (data[i + 2] != 0xFF || data[i + 3] != 0xFF || data[i + 4] != 0xFF)
                return false;

            return true;
        }

        private static bool SearchBack(ref byte[] data, ulong i, Func<byte, bool> matcher, out ulong pos)
        {
            pos = 0;
            long cursor = 0;

            while (!matcher(data[i + (ulong)cursor-- - 1]))
            {
                if (Math.Abs(cursor) >= 0x2000)
                    return false;
            }

            pos = i + (ulong)cursor;
            return true;
        }

        private static bool SearchForward(ref byte[] data, ulong i, Func<byte, bool> matcher, out ulong pos)
        {
            pos = 0;
            long cursor = 0;

            while (!matcher(data[i + (ulong)cursor++ + 1]))
            {
                if (Math.Abs(cursor) >= 0x2000)
                    return false;
            }

            pos = i + (ulong)cursor;
            return true;
        }

    }
}
