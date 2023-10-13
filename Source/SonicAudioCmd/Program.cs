using SonicAudioLib.CriMw;

namespace SonicAudioCmd
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                CriTable criTable = new CriTable();
                criTable.Load(args[i]);
                criTable.Rows[0]["AcbVolume"] = 0.4f;
                criTable.WriterSettings = CriTableWriterSettings.Adx2Settings;
                criTable.Save(args[i]);
            }
        }
    }
}