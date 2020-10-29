namespace SaviAccess
{
    public class GeneralSettings
    {
        public string Delimiter { get; set; }
        public bool GenerateSasCode { get; set; } = true;
        public bool GenerateHeaders { get; set; } = true;
        public string WorkDirectory { get; set; }
        public bool WriteDebugInformation { get; set; } = true;
    }
}