using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CafeMsgExtractor
{
    internal class Program
    {
        private static readonly string[] languages = { "ja", "en", "fr", "de", "it", "es", "ko", "zh" };

        private static string inputDir, outputDir;
        
        private static readonly string substringSeparator = "message";
        private static readonly Encoding languageEncoder = Encoding.GetEncoding("ISO-8859-1");

        private static void Error()
        {
            Console.Out.WriteLine("Invalid arguments.");
            Console.Out.WriteLine("Usage: CafeMsgExtractor.exe <extractedArchivesDir> <outputDir>");
        }

        public static void Main(string[] args)
        {
            if (args.Length != 2 || !Directory.Exists(args[0]) || !Directory.Exists(args[1]))
            {
                Error();
                return;
            }

            inputDir = args[0];
            outputDir = args[1];

            string[] subDirs = Directory
                .GetDirectories(inputDir)
                .Where(dir => Directory.GetFiles(dir, "*.msg").Length > 0)
                .ToArray();

            string[] subCategories = subDirs.Select(dir => Path.GetFileNameWithoutExtension(dir).Substring(0, 6))
                .Distinct().ToArray();

            Dictionary<string, string[]> categoriesDict = new Dictionary<string, string[]>();

            foreach (string subCategory in subCategories)
            {
                List<string> categoryDirectories = subDirs
                    .Where(dir => Path.GetFileNameWithoutExtension(dir).Substring(0, 6) == subCategory)
                    .ToList();

                categoryDirectories.Sort((lhs, rhs) => Convert.ToInt32(Path.GetFileNameWithoutExtension(lhs), 16) -
                                                       Convert.ToInt32(Path.GetFileNameWithoutExtension(rhs), 16));

                if (categoryDirectories.Count > 1)
                {
                    Console.Out.WriteLine("\nCategory : " + subCategory);
                    Console.Out.WriteLine("Size : " + categoryDirectories.Count);
                    Console.Out.WriteLine("Directories :\n" + string.Join("\n", categoryDirectories));

                    categoriesDict.Add(subCategory, categoryDirectories.ToArray());

                    subDirs = subDirs.Except(categoryDirectories).ToArray();
                }
            }

            Console.Out.WriteLine("\nCategory : Misc.");
            Console.Out.WriteLine("Size : " + subDirs.Length);
            Console.Out.WriteLine("Directories :\n" + string.Join("\n", subDirs));

            foreach (KeyValuePair<string, string[]> keyValuePair in categoriesDict)
            {
                string category = keyValuePair.Key;
                string[] directories = keyValuePair.Value;

                Console.Out.WriteLine("\n\nExtracting Category : " + category);
                for (int i = 0; i < directories.Length; i++)
                {
                    ExtractCategory(category, directories[i], languages[i]);
                }
            }

            foreach (string subDir in subDirs)
            {
                Console.Out.WriteLine("\n\nExtracting Misc files.");
                ExtractMisc("_misc", subDir);
            }
            
            Console.Out.WriteLine("\nDone extracting message files!");

        }

        public static void ExtractMisc(string categoryName, string directory)
        {
            string subDirName = Path.GetFileNameWithoutExtension(directory);
            DirectoryInfo outDir = Directory.CreateDirectory(Path.Combine(outputDir, categoryName, subDirName));
            
            Console.Out.WriteLine("\nDirectory : " + categoryName + "/" + subDirName);
            Console.Out.WriteLine("Files :");

            ExtractFiles(directory, outDir);
        }

        public static void ExtractCategory(string categoryName, string directory, string language)
        {
            string subDirName = Path.GetFileNameWithoutExtension(categoryName);
            DirectoryInfo outDir = Directory.CreateDirectory(Path.Combine(outputDir, language));
            
            Console.Out.WriteLine("\nDirectory : " + subDirName + "/" + language);
            Console.Out.WriteLine("Files :");

            ExtractFiles(directory, outDir);
        }

        public static void ExtractFiles(string directory, DirectoryInfo outDir)
        {
            string[] files = Directory.GetFiles(directory, "*.msg");
            
            foreach (string file in files)
            {
                byte[] bytesFile = File.ReadAllBytes(file).Skip(96).ToArray();
                    
                string text = Regex.Replace(languageEncoder.GetString(bytesFile), @"\x00", "");

                string fileName;
                    
                if (text.LastIndexOf(substringSeparator, StringComparison.Ordinal) > -1)
                {
                    fileName = Regex.Match(
                        text.Substring(text.LastIndexOf(substringSeparator, StringComparison.Ordinal) + substringSeparator.Length),
                        @"^([\w]+)").Value;

                    text = text.Substring(0, text.LastIndexOf(substringSeparator, StringComparison.Ordinal));
                }
                else
                {
                    fileName = Path.GetFileNameWithoutExtension(file);
                }

                fileName += ".txt";

                string newFile = Path.Combine(outDir.FullName, fileName);

                Console.Out.WriteLine("- " + newFile);
                    
                if (File.Exists(newFile))
                {
                    File.Delete(newFile);
                }

                File.WriteAllText(newFile, text);
                    
            }
        }
    }
}