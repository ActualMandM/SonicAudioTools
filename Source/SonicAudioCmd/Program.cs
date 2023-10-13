using System;
using System.IO;

using SonicAudioLib.Archives;
using SonicAudioLib.CriMw;
using SonicAudioLib.IO;

namespace SonicAudioCmd
{
    class Program
    {
        static string FilePath = string.Empty;
        static ulong KeyCode = 0;

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine($"Usage: {System.Diagnostics.Process.GetCurrentProcess().ProcessName} AcbFolder KeyCode");
                return;
            }

            FilePath = args[0];
            KeyCode = ulong.Parse(args[1]);

            string[] FileList = Directory.GetFiles(FilePath);
            string OutputPath = Path.Combine(FilePath, "StreamTool");

            foreach (string AcbFile in FileList)
            {
                if (Path.GetExtension(AcbFile) == ".acb")
                {
                    string AcbName = Path.GetFileNameWithoutExtension(AcbFile);
                    ushort AwbHash = 0;

                    using (CriTableReader AcbReader = CriTableReader.Create(AcbFile))
                    {
                        AcbReader.Read();

                        CriAfs2Archive Afs2Archive = new CriAfs2Archive();

                        // Internal ACB
                        if (AcbReader.GetLength("AwbFile") > 0)
                        {
                            using (SubStream afs2Stream = AcbReader.GetSubStream("AwbFile"))
                            {
                                if (CheckIfAfs2(afs2Stream))
                                    Afs2Archive.Read(afs2Stream);
                            }

                            AwbHash = Afs2Archive.SubKey;
                        }

                        // External ACB
                        if (AcbReader.GetLength("StreamAwbAfs2Header") > 0)
                        {
                            using (SubStream extAfs2Stream = AcbReader.GetSubStream("StreamAwbAfs2Header"))
                            {
                                bool utfMode = DataStream.ReadCString(extAfs2Stream, 4) == "@UTF";
                                extAfs2Stream.Seek(0, SeekOrigin.Begin);

                                if (utfMode)
                                {
                                    using (CriTableReader utfAfs2HeaderReader = CriTableReader.Create(extAfs2Stream))
                                    {
                                        utfAfs2HeaderReader.Read();

                                        using (SubStream extAfs2HeaderStream = utfAfs2HeaderReader.GetSubStream("Header"))
                                        {
                                            Afs2Archive.Read(extAfs2HeaderStream);
                                        }
                                    }
                                }
                                else
                                {
                                    Afs2Archive.Read(extAfs2Stream);
                                }
                            }

                            AwbHash = Afs2Archive.SubKey;
                        }
                    }

                    if (AwbHash == 0)
                        continue;

                    ulong AcbKey = KeyCode * (((ulong)AwbHash << 16) | (ushort)(~AwbHash + 2));

                    if (!Directory.Exists(OutputPath))
                        Directory.CreateDirectory(OutputPath);

                    string STData = "@echo off\r\n" +
                        "cd /d \"%~dp0\"\r\n" +
                        "cd ..\r\n" +
                        "vgmstream -l 1 -f 0 -L -o \"%~n1.wav\" \"%~1\"\r\n" +
                        "move \"%~n1.wav\" \".\"\r\n" +
                        "vgaudio --hcaquality High --keycode " + AcbKey + " \"%~n1.wav\" \"%~n1.hca\"\r\n" +
                        "del \"%~n1.wav\"";

                    File.WriteAllText(Path.Combine(OutputPath, AcbName + ".bat"), STData);
                }
            }
        }

        static bool CheckIfAfs2(Stream source)
        {
            long oldPosition = source.Position;
            bool result = DataStream.ReadCString(source, 4) == "AFS2";
            source.Seek(oldPosition, SeekOrigin.Begin);

            return result;
        }
    }
}
