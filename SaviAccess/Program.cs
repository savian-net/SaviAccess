using CommandLine;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CommandLine.Text;
using Serilog;
using Microsoft.Extensions.Configuration;

namespace SaviAccess
{
    class Program
    {
        public static AppConfiguration Configuration { get; set; } = new AppConfiguration();
        public static IConfiguration ConfigurationEngine { get; set; }

        public class Options
        {
            [Usage(ApplicationAlias = "SaviAccess")]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    yield return new Example("[OPTIONAL] STEP 1: Code Generation Example", new Options
                    {
                        GenerateSasCodeFile = true,
                        SqlQuery = "SELECT * FROM [WideWorldImporters].[Sales].[Invoices]",
                        TableName = "Invoices",
                        OdbcConnectionString = @"driver=ODBC Driver 17 for SQL Server;Server=MyServer;Database=WideWorldImporters;Trusted_Connection=Yes;",
                        Delimiter = "|",
                        Headers = true,
                        WorkDirectory = @"C:\Temp"
                    });

                    yield return new Example("STEP 2: Read Data Example", new Options
                    {
                        GenerateSasCodeFile = false,
                        SqlQuery = "SELECT * FROM [WideWorldImporters].[Sales].[Invoices]",
                        TableName = "Invoices",
                        OdbcConnectionString = @"driver=ODBC Driver 17 for SQL Server;Server=MyServer;Database=WideWorldImporters;Trusted_Connection=Yes;",
                        Delimiter = "|",
                        Headers = true,
                        WorkDirectory = @"C:\Temp"
                    });

                    yield return new Example("Debug Data Example", new Options
                    {
                        GenerateSasCodeFile = false,
                        SqlQuery = "SELECT * FROM [WideWorldImporters].[Sales].[Invoices]",
                        TableName = "Invoices",
                        OdbcConnectionString = @"driver=ODBC Driver 17 for SQL Server;Server=MyServer;Database=WideWorldImporters;Trusted_Connection=Yes;",
                        Delimiter = "|",
                        Headers = true,
                        WorkDirectory = @"C:\Temp",
                        Debug = true
                    });
                }
            }


            [Option('s', "sas", Required = false, HelpText = "Indicates that a sample SAS code will be generated that can read the file. This is the flag that indicates whether data will be sent or only the SAS code generation process will run.")]
            public bool? GenerateSasCodeFile { get; set; }

            [Option('q', "sql", Required = true, HelpText = "SQL query for the database")]
            public string SqlQuery { get; set; }

            [Option('t', "table", Required = true, HelpText = "The name of the resulting table")]
            public string TableName { get; set; }

            [Option('o', "odbc", Required = false, HelpText = "The odbc connection string")]
            public string OdbcConnectionString { get; set; }

            [Option('d', "dlm", Required = false, HelpText = "The delimited to use. Default is tab-delimited.")]
            public string Delimiter { get; set; }

            [Option('h', "headers", Required = false, HelpText = "Whether headers are generated. Default is false.",
                Default = false)]
            public bool? Headers { get; set; }

            [Option('w', "WorkDirectory", Required = false, HelpText = "Directory location where logs and sample SAS code will be generated.",
                Default = "")]
            public string WorkDirectory { get; set; }

            [Option('b', "Debug", Required = false, HelpText = "Writes the generated data to a file")]
            public bool? Debug { get; set; }
        }

        public static Options _options;

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<Options>((errs) => HandleParseError(errs));
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            //Console.WriteLine("Error in command line options:");
            foreach (var err in errs)
            {
                //Console.WriteLine(err.Tag.ToString());
                throw new Exception(err.Tag.ToString());
            }

        }

        private static void RunOptionsAndReturnExitCode(Options opts)
        {
            _options = opts;
            ReadConfig();
            ResolveDiscrepenciesWithOptions();
            ConfigureLogging();
            LogOptions(_options);
            var dt = GetDataTableFromODBC();
            if (dt == null)
            {
                Log.Error("No data found.");
                return;
            }
            if (!Configuration.General.GenerateSasCode)
            {
                WriteDataTable(dt, Console.OpenStandardOutput());
            }
            //var tw = Console.OpenStandardOutput();

            if (Configuration.General.WriteDebugInformation)
            {
                var fs = new FileStream(Path.Combine(_options.WorkDirectory, $"{_options.TableName}_SampleRead.data"),
                    FileMode.Create);
                WriteDataTable(dt, fs);
            }
            if (Configuration.General.GenerateSasCode)
            {
                GenerateSasCode(dt);
            }
        }

        private static void ResolveDiscrepenciesWithOptions()
        {
            Configuration.General.WriteDebugInformation = _options.Debug ?? Configuration.General.WriteDebugInformation;
            Configuration.General.GenerateHeaders = _options.Headers ?? Configuration.General.GenerateHeaders;
            Configuration.General.GenerateSasCode = _options.GenerateSasCodeFile ?? Configuration.General.GenerateSasCode;
        }

        private static void ReadConfig()
        {
            var builder = new ConfigurationBuilder()
                  .AddJsonFile(@"AppSettings.json")
                  ;
            ConfigurationEngine = builder.Build();
            ConfigurationEngine.Bind(Configuration);
        }

        public static void WriteDataTable(DataTable dataTable, Stream stream)
        {
            using (var writer = stream)
            using (var streamWriter = new StreamWriter(writer))
            using (var csvWriter = new CsvWriter(streamWriter))
            {
                csvWriter.Configuration.Delimiter = _options.Delimiter;
                WriteRecords(dataTable, csvWriter);
            }
        }

        private static void WriteRecords(DataTable dataTable, CsvWriter csvWriter)
        {
            try
            {
                Log.Information($"Writing {dataTable.Rows.Count} records");
                foreach (DataColumn column in dataTable.Columns)
                {
                    csvWriter.WriteField(column.ColumnName);
                }

                csvWriter.NextRecord();

                foreach (DataRow row in dataTable.Rows)
                {
                    for (var i = 0; i < dataTable.Columns.Count; i++)
                    {
                        csvWriter.WriteField(row[i]??"");
                    }

                    csvWriter.NextRecord();
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Failed to write records");
            }
        }

        private static void GenerateSasCode(DataTable dt)
        {
            try
            {
                if (!Directory.Exists(_options.WorkDirectory))
                {
                    Log.Information("Creating work directory.");
                    Directory.CreateDirectory(_options.WorkDirectory);
                    Log.Information("Work directory created.");
                }

                using (var sw =
                    new StreamWriter(Path.Combine(_options.WorkDirectory, $"{_options.TableName}_SampleRead.sas")))
                {
                    Log.Information("Generating SAS code.");
                    sw.AutoFlush = true;
                    //-q "SELECT * FROM [WideWorldImporters].[Sales].[Invoices]" -t "Invoices" -o "driver=ODBC Driver 17 for SQL Server;Server=ALAN-PC;Database=WideWorldImporters;Trusted_Connection=Yes;" -s true -w "z:\scratch"
                    var dq = "\"\"";
                    sw.WriteLine(@"/*=======================================================*");
                    sw.WriteLine(@" | MAKE CHANGES TO THE BELOW TO REFLECT YOUR ENVIRONMENT |");
                    sw.WriteLine(@" *=======================================================*/");
                    sw.WriteLine();
                    sw.Write($"FILENAME DATAPIPE PIPE ");
                    sw.Write($@"""c:\temp\SaviAccess.exe ");
                    sw.Write($@"-s false ");
                    sw.Write($@"-q {dq}{_options.SqlQuery}{dq} ");
                    sw.Write($@"-t {dq}{_options.TableName}{dq} ");
                    sw.Write($@"-o {dq}{_options.OdbcConnectionString}{dq} ");
                    sw.WriteLine("\";");
                    sw.WriteLine();
                    sw.WriteLine($"DATA {_options.TableName} / VIEW={_options.TableName};");
                    var dlm = _options.Delimiter == "\t" ? "'09'x" : $"'{_options.Delimiter}'";
                    sw.WriteLine($"   INFILE DATAPIPE DLM={dlm} DSD MISSOVER FIRSTOBS=2;");
                    sw.WriteLine($"   INPUT");
                    foreach (DataColumn col in dt.Columns)
                    {
                        sw.WriteLine($"         {col.ColumnName.ToUpper()} {GetSasInputDefinition(col.DataType.Name)}");
                    }

                    sw.WriteLine("   ;");
                    sw.WriteLine("RUN;");
                    Log.Information("Finished generating SAS code.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to generate SAS code");
                throw ex;
            }
        }

        private static object GetSasInputDefinition(string name)
        {
            switch (name.ToLower())
            {
                case "char":
                case "string":
                    return "$";
                case "datetime":
                    return "anydtdte12.";
                default:
                    Log.Error($"Found input definition that did not match: {name.ToLower()}");
                    return "";
            }
        }

        private static DataTable GetDataTableFromODBC()
        {
            var m = 0.181732272 * 27513;
            Log.Information($"Get datatable from ODBC. This is capped at {m} records. Contact http://www.savian.net if more than {m} records are needed.");
            var tableName = _options.TableName;
            var dt = new DataTable($"{tableName.ToUpper()}");
            using (var conn = new OdbcConnection(_options.OdbcConnectionString))
            {
                //conn.ConnectionTimeout = 20000000;
                try
                {
                    using (var da = new OdbcDataAdapter(_options.SqlQuery, conn))
                    {
                        //da.SelectCommand.CommandTimeout = 120000;
                        da.Fill(0, (int) m, dt);
                    }
                    Log.Information($"Finished getting datatable from ODBC.");
                    Log.Information($"Total records: {dt.Rows.Count}");
                    Log.Information($"Total columns: {dt.Columns.Count}");
                    return dt;
                }
                catch (Exception e)
                {
                    Log.Error("Error getting datatable from ODBC.", e);
                    return null;
                }
            }
        }

        private static void ConfigureLogging()
        {
            try
            {
                Directory.CreateDirectory(Program._options.WorkDirectory);
                var outLoc = Path.Combine(Program._options.WorkDirectory, System.AppDomain.CurrentDomain.FriendlyName);
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(rollingInterval: RollingInterval.Day,
                        fileSizeLimitBytes: 100000,
                        rollOnFileSizeLimit: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1),
                        path: $@"{outLoc}.log",
                        shared: true
                    )
                    .CreateLogger();
                Log.Information("Logging started");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        internal static void LogOptions<T>(T _options)
        {
            var props = typeof(T).GetProperties();
            foreach (var prop in props)
            {
                Log.Information($"{prop.Name} : {prop.GetValue(_options)}");
            }
        }
    }
}