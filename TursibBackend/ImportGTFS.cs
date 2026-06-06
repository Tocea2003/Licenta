using System;
using Microsoft.Extensions.Configuration;

namespace TursibBackend
{
    class ImportGTFS
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🚌 Tursib GTFS Importer");
            Console.WriteLine("========================\n");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var gtfsPath = Environment.GetEnvironmentVariable("GTFS_PATH")
                ?? configuration["GtfsPath"]
                ?? @"D:\Licenta\tursib.gtfs_2025-10-v1";

            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("❌ Connection string not found in appsettings.json");
                return;
            }

            if (!System.IO.Directory.Exists(gtfsPath))
            {
                Console.WriteLine($"❌ GTFS directory not found: {gtfsPath}");
                return;
            }

            try
            {
                var importer = new GTFSImporter(connectionString, gtfsPath);
                importer.ImportAll();
                
                Console.WriteLine("\n🎉 Import completed successfully!");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error during import: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
