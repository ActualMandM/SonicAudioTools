using System;
using System.IO;
using System.Text;
using SonicAudioLib.Archives;
using SonicAudioLib.CriMw;
using SonicAudioLib.IO;
using static System.Net.Mime.MediaTypeNames;

namespace SonicAudioCmd
{
    class Program
    {
        static string BasePath = string.Empty;
        static string OutputPath = string.Empty;
        static ulong KeyCode = 0;

        static StringBuilder KeyData = new StringBuilder();

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine($"Usage: {System.Diagnostics.Process.GetCurrentProcess().ProcessName} AcbFolder KeyCode");
                return;
            }

            KeyCode = ulong.Parse(args[1]);

            if (File.GetAttributes(args[0]).HasFlag(FileAttributes.Directory))
            {
                BasePath = args[0];
                OutputPath = args[0];
                SearchDirectories(args[0]);

                File.WriteAllText(BasePath + "_keys.txt", KeyData.ToString());
            }
            else
            {
                Console.WriteLine("Please provide a path to the ACB folder.");
                return;
            }
        }

        static void SearchDirectories(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                GenerateFile(file);
            }

            foreach (string folder in Directory.GetDirectories(path))
            {
                OutputPath = folder;
                SearchDirectories(folder);
            }
        }

        static void GenerateFile(string filePath)
        {
            if (Path.GetExtension(filePath) == ".acb")
            {
                ushort AwbHash = 0;

                using (CriTableReader AcbReader = CriTableReader.Create(filePath))
                {
                    AcbReader.Read();

                    CriAfs2Archive Afs2Archive = new CriAfs2Archive();

                    // Internal ACB
                    if (AcbReader.GetLength("AwbFile") > 0)
                    {
                        using (SubStream Afs2Stream = AcbReader.GetSubStream("AwbFile"))
                        {
                            if (CheckIfAfs2(Afs2Stream))
                                Afs2Archive.Read(Afs2Stream);
                        }
                    }

                    // External ACB
                    else if (AcbReader.GetLength("StreamAwbAfs2Header") > 0)
                    {
                        using (SubStream ExtAfs2Stream = AcbReader.GetSubStream("StreamAwbAfs2Header"))
                        {
                            bool UtfMode = DataStream.ReadCString(ExtAfs2Stream, 4) == "@UTF";
                            ExtAfs2Stream.Seek(0, SeekOrigin.Begin);

                            if (UtfMode)
                            {
                                using (CriTableReader UtfAfs2HeaderReader = CriTableReader.Create(ExtAfs2Stream))
                                {
                                    UtfAfs2HeaderReader.Read();

                                    using (SubStream ExtAfs2HeaderStream = UtfAfs2HeaderReader.GetSubStream("Header"))
                                    {
                                        Afs2Archive.Read(ExtAfs2HeaderStream);
                                    }
                                }
                            }
                            else
                            {
                                Afs2Archive.Read(ExtAfs2Stream);
                            }
                        }
                    }

                    if (Afs2Archive.SubKey != 0)
                        AwbHash = Afs2Archive.SubKey;
                }

                if (AwbHash == 0)
                    return;

                ulong AcbKey = KeyCode * (((ulong)AwbHash << 16) | (ushort)(~AwbHash + 2));

                KeyData.AppendLine($"{filePath}: {AcbKey}");
            }
        }

        static bool CheckIfAfs2(Stream Source)
        {
            long OldPosition = Source.Position;
            bool Result = DataStream.ReadCString(Source, 4) == "AFS2";
            Source.Seek(OldPosition, SeekOrigin.Begin);

            return Result;
        }
    }
}
