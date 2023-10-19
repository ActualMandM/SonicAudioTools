using System;
using System.IO;

using SonicAudioLib.Archives;
using SonicAudioLib.CriMw;
using SonicAudioLib.IO;

namespace SonicAudioCmd
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine($"Usage: {System.Diagnostics.Process.GetCurrentProcess().ProcessName} AcbFolder KeyCode");
                return;
            }

            string AcbFolder = args[0];
            ulong KeyCode = ulong.Parse(args[1]);

            string[] AcbList = Directory.GetFiles(AcbFolder);
            string OutputPath = Path.Combine(AcbFolder, "LoopingAudioConverter");

            foreach (string AcbFile in AcbList)
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
                        continue;

                    ulong AcbKey = KeyCode * (((ulong)AwbHash << 16) | (ushort)(~AwbHash + 2));

                    if (!Directory.Exists(OutputPath))
                        Directory.CreateDirectory(OutputPath);

                    string LACData = "<?xml version=\"1.0\"?>\r\n" +
                        "<Options xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r\n" +
                        "  <OutputDir>./output</OutputDir>\r\n" +
                        "  <InputDir />\r\n" +
                        "  <Channels xsi:nil=\"true\" />\r\n" +
                        "  <SampleRate xsi:nil=\"true\" />\r\n" +
                        "  <AmplifydB xsi:nil=\"true\" />\r\n" +
                        "  <AmplifyRatio xsi:nil=\"true\" />\r\n" +
                        "  <PitchSemitones xsi:nil=\"true\" />\r\n" +
                        "  <TempoRatio xsi:nil=\"true\" />\r\n" +
                        "  <DefaultInputDuration />\r\n" +
                        "  <ChannelSplit>Pairs</ChannelSplit>\r\n" +
                        "  <ExporterType>VGAudio_HCA</ExporterType>\r\n" +
                        "  <AACEncodingParameters />\r\n" +
                        "  <OggVorbisEncodingParameters />\r\n" +
                        "  <MP3FFmpegParameters />\r\n" +
                        "  <AACFFmpegParameters />\r\n" +
                        "  <AdxOptions>\r\n" +
                        "    <TrimFile>true</TrimFile>\r\n" +
                        "    <Version>4</Version>\r\n" +
                        "    <FrameSize>18</FrameSize>\r\n" +
                        "    <Filter>2</Filter>\r\n" +
                        "    <Type>Linear</Type>\r\n" +
                        "    <EncryptionType>KeyCode</EncryptionType>\r\n" +
                        "    <KeyCode>" + KeyCode + "</KeyCode>\r\n" +
                        "  </AdxOptions>\r\n" +
                        "  <HcaOptions>\r\n" +
                        "    <TrimFile>true</TrimFile>\r\n" +
                        "    <Quality>Highest</Quality>\r\n" +
                        "    <LimitBitrate>false</LimitBitrate>\r\n" +
                        "    <Bitrate>0</Bitrate>\r\n" +
                        "    <KeyCode>" + AcbKey + "</KeyCode>\r\n" +
                        "  </HcaOptions>\r\n" +
                        "  <BxstmOptions>\r\n" +
                        "    <TrimFile>true</TrimFile>\r\n" +
                        "    <RecalculateSeekTable>true</RecalculateSeekTable>\r\n" +
                        "    <RecalculateLoopContext>true</RecalculateLoopContext>\r\n" +
                        "    <SamplesPerInterleave>14336</SamplesPerInterleave>\r\n" +
                        "    <SamplesPerSeekTableEntry>14336</SamplesPerSeekTableEntry>\r\n" +
                        "    <LoopPointAlignment>14336</LoopPointAlignment>\r\n" +
                        "    <Codec>GcAdpcm</Codec>\r\n" +
                        "    <Endianness xsi:nil=\"true\" />\r\n" +
                        "    <Version>\r\n" +
                        "      <UseDefault>true</UseDefault>\r\n" +
                        "      <Major>0</Major>\r\n" +
                        "      <Minor>0</Minor>\r\n" +
                        "      <Micro>0</Micro>\r\n" +
                        "      <Revision>0</Revision>\r\n" +
                        "    </Version>\r\n" +
                        "    <TrackType>Standard</TrackType>\r\n" +
                        "    <SeekTableType>Standard</SeekTableType>\r\n" +
                        "  </BxstmOptions>\r\n" +
                        "  <WaveEncoding>PCM8</WaveEncoding>\r\n" +
                        "  <InputLoopBehavior>NoChange</InputLoopBehavior>\r\n" +
                        "  <ExportWholeSong>true</ExportWholeSong>\r\n" +
                        "  <WholeSongExportByDesiredDuration>false</WholeSongExportByDesiredDuration>\r\n" +
                        "  <WholeSongSuffix />\r\n" +
                        "  <NumberOfLoops>1</NumberOfLoops>\r\n" +
                        "  <DesiredDuration>900</DesiredDuration>\r\n" +
                        "  <FadeOutSec>0</FadeOutSec>\r\n" +
                        "  <ExportPreLoop>false</ExportPreLoop>\r\n" +
                        "  <PreLoopSuffix> (beginning)</PreLoopSuffix>\r\n" +
                        "  <ExportLoop>false</ExportLoop>\r\n" +
                        "  <LoopSuffix> (loop)</LoopSuffix>\r\n" +
                        "  <ExportPostLoop>false</ExportPostLoop>\r\n" +
                        "  <PostLoopSuffix> (end)</PostLoopSuffix>\r\n" +
                        "  <ExportLastLap>false</ExportLastLap>\r\n" +
                        "  <LastLapSuffix> (final lap)</LastLapSuffix>\r\n" +
                        "  <BypassEncoding>false</BypassEncoding>\r\n" +
                        "</Options>";

                    File.WriteAllText(Path.Combine(OutputPath, AcbName + ".xml"), LACData);
                }
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
