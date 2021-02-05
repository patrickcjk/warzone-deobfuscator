using System;
using System.IO;

namespace warfare_dump_fixer
{
    class Program
    {
        private static long offsetIDABase = 0x1000;
        private static long offsetFileBase = 0x600;

        public static ulong DebugFileOffsetToIDA(ulong value)
        {
            return (value - (ulong)offsetFileBase) + (ulong)offsetIDABase;
        }

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("[###]> Incorrect usage, please drag & drop the modern warfare dump on this executable");
                Console.Read();
                return;
            }

            string file_path = args[0];
            Console.WriteLine("[###]> Dump path: {0}", file_path);

            if (!File.Exists(file_path))
            {
                Console.WriteLine("[###]> File does not exist");
                Console.Read();
                return;
            }

            byte[] raw_data = File.ReadAllBytes(file_path);

            for (ulong i = 0; i < (ulong)raw_data.Length; i++)
            {
                if (i < (ulong)offsetFileBase)
                    continue; //Base of the first .txt section, bases on the file dump

                JumpBack(ref raw_data, i);
                JumpNear(ref raw_data, i);
            }

            string new_path = Path.Combine(Path.GetDirectoryName(file_path), Path.GetFileNameWithoutExtension(file_path) + "_fixed.exe");
            File.WriteAllBytes(new_path, raw_data);

            Console.WriteLine("[###]> Saved fixed dump at: {0}", new_path);

            Console.WriteLine("[###]> Done!");
            Console.Read();
        }

        static void JumpNear(ref byte[] bytes, ulong i)
        {
            if (!(bytes[i] == 0x74 && bytes[i + 1] == 0x40))
                return;

            if (bytes[i + 0x42] != 0xE9)
                return;

            for (ulong j = 2; j < 0x42; j++)
                bytes[i + j] = 0x90;

            Console.WriteLine("JumpNear Found at : " + DebugFileOffsetToIDA(i).ToString("X16"));
        }

        static void JumpBack(ref byte[] bytes, ulong i)
        {
            if (bytes[i] != 0xE8)
                return;

            if (bytes[i + 1] >> 4 != 0xf)
                return;

            if (bytes[i + 2] != 0xFF || bytes[i + 3] != 0xFF || bytes[i + 4] != 0xFF)
                return;

            ulong StartOfsInstruction = 0; //Start Ofs of pushfq/push

            for (int y = -1; y > -20; y--)
            {
                var currIndex = ((long)i + y);

                if (
                    (bytes[currIndex] == 0x9C && (bytes[currIndex - 1] == 0x50 || bytes[currIndex - 1] == 0x51 || bytes[currIndex - 1] == 0x52 || bytes[currIndex - 1] == 0x53)) ||
                    ((bytes[currIndex] == 0x50 || bytes[currIndex] == 0x51 || bytes[currIndex] == 0x52 || bytes[currIndex] == 0x53) && bytes[currIndex - 1] == 0x9C))
                {
                    StartOfsInstruction = (ulong)currIndex - 1;
                }
            }

            if (StartOfsInstruction == 0)
                return;

            var JumpInstruction = (0xff - bytes[i + 1]) - 0x5; //0x5 size of the current instruction
            var RealJumpStartOFs = i - (ulong)(JumpInstruction + 0x1);
            if (bytes[RealJumpStartOFs] != 0xEB)
                return; //If not 0xEB we leave

            ulong RealJumpCount = bytes[RealJumpStartOFs + 1];

            Console.WriteLine(DebugFileOffsetToIDA(StartOfsInstruction + 0x2).ToString("X16") + " - " +
                              DebugFileOffsetToIDA((ulong)RealJumpStartOFs + RealJumpCount - 0x1 + 0x2)
                                  .ToString("X16"));

            for (ulong a = StartOfsInstruction + 0x2; a < (ulong)RealJumpStartOFs + RealJumpCount + 0x2; a++) //0x2 est la taille du jump
            {
                bytes[a] = 0x90;
            }

            Console.WriteLine("JumpBack Found at : " + DebugFileOffsetToIDA(i).ToString("X16"));
        }
    }
}
