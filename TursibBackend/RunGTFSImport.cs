using System;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;

namespace TursibBackend
{
    public class RunGTFSImport
    {
        public static void ExecuteImport(string? overridePath = null)
        {
            Console.WriteLine("🚌 Tursib GTFS Importer");
            Console.WriteLine("========================\n");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var gtfsPath = overridePath
                ?? Environment.GetEnvironmentVariable("GTFS_PATH")
                ?? configuration["GtfsPath"]
                ?? @"D:\Licenta\tursib.gtfs_2025-10-v1";

            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("❌ Connection string not found in appsettings.json");
                return;
            }

            if (!Directory.Exists(gtfsPath))
            {
                Console.WriteLine($"❌ GTFS directory not found: {gtfsPath}");
                return;
            }

            try
            {
                var importer = new GTFSImporter(connectionString, gtfsPath);
                importer.ImportAll();
                
                Console.WriteLine("\n🎉 Import completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error during import: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
