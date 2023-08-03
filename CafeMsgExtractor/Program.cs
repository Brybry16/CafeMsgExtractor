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
        private static readonly string[] languages = { "ja", "en", "fr", "de", "it", "es", "ko", "zh", "_misc" };

        private const uint HEADER_MAGIC = 0xF6542E8D;

        private static readonly byte[] MSG_MAGIC = {
            0x6D, 0x65, 0x73, 0x73, 0x61, 0x67, 0x65, 0x00
        };

        private static string inputDir, outputDir;
        
        private static readonly string substringSeparator = "message ";
        private static readonly Encoding languageEncoder = Encoding.GetEncoding("utf-8");

        private static void Error()
        {
            Console.Out.WriteLine("Invalid arguments.");
            Console.Out.WriteLine("Usage: CafeMsgExtractor.exe <extractedArchivesDir> <outputDir>");
        }

        public static void Main(string[] args)
        {
            if (args.Length != 2 || !Directory.Exists(args[0]) || !Directory.Exists(args[1]))
            {
                #if DEBUG
                #else
                    Error();
                    return;
                #endif
            }
            else
            {
                inputDir = args[0];
                outputDir = args[1];
            }

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
                    int l = i >= languages.Length ? languages.Length - 1 : i;
                    ExtractCategory(category, directories[i], languages[l]);
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
            Console.Out.WriteLine("OutDir : " + outDir.FullName);
            Console.Out.WriteLine("Files :");

            ExtractFiles(directory, outDir, languageEncoder);
            //CopyFiles(directory, outDir);
        }

        public static void ExtractCategory(string categoryName, string directory, string language)
        {

            Encoding encoder = languageEncoder;
            
            string subDirName = Path.GetFileNameWithoutExtension(categoryName);
            DirectoryInfo outDir = Directory.CreateDirectory(Path.Combine(outputDir, language));
            
            Console.Out.WriteLine("\nDirectory : " + subDirName + "/" + language);
            Console.Out.WriteLine("OutDir : " + outDir.FullName);
            Console.Out.WriteLine("Files :");

            ExtractFiles(directory, outDir, encoder);
            //CopyFiles(directory, outDir);
        }

        public static void CopyFiles(string directory, DirectoryInfo outDir)
        {
            string[] files = Directory.GetFiles(directory, "*.msg");

            foreach (string file in files)
            {
                
                string fileName = Path.GetFileName(file);
                
                Console.Out.WriteLine("- " + fileName);

                string outPath = Path.Combine(outDir.FullName, Path.GetFileName(directory));
                Directory.CreateDirectory(outPath);

                string newFile = Path.Combine(outPath, fileName);
                
                if (File.Exists(newFile))
                {
                    File.Delete(newFile);
                }

                File.Copy(file, newFile);
            }
        }

        public static void ExtractFiles(string directory, DirectoryInfo outDir, Encoding encoder)
        {
            string[] files = Directory.GetFiles(directory, "*.msg");
            List<string> fileNames = new List<string>();
            bool isMessage;

            Directory.CreateDirectory(Path.Combine(outDir.FullName, "messages"));
            
            foreach (string file in files)
            {
                isMessage = false;
                
                byte[] headerBytes = File.ReadAllBytes(file);
                byte[] bytesFile;
                byte[] footerBytes = null;
                string text;
                
                BoyerMoore searcher = new BoyerMoore(MSG_MAGIC);
                var indexes = searcher.Search(headerBytes);

                if (indexes.Any())
                {
                    footerBytes = headerBytes.Skip(indexes.Last()).ToArray();
                    bytesFile = headerBytes.Skip(96).Take(indexes.Last() - 96).ToArray();
                    text = Encoding.Unicode.GetString(bytesFile);
                }
                else
                {
                    bytesFile = headerBytes.Skip(96).ToArray();
                    text = languageEncoder.GetString(bytesFile);
                }
                
                headerBytes = headerBytes.Take(96).ToArray();

                string fileName = "";
                if (footerBytes != null)
                {
                    string footerText = languageEncoder.GetString(footerBytes);
                    
                    fileName = Regex.Match(
                        footerText.Substring(substringSeparator.Length),
                        @"^([\w]+)").Value;

                    isMessage = true;
                }
                else if(BitConverter.ToUInt32(headerBytes, 0) == HEADER_MAGIC)
                {
                    int i = 0;

                    char[] invalidChars = Path.GetInvalidPathChars().Union(Path.GetInvalidFileNameChars()).ToArray();
                    
                    while (bytesFile[i] != 0x00 || fileName.Length == 0)
                    {
                        char c = Convert.ToChar(bytesFile[i]);
                        i++;
                        
                        if(fileName.Length == 0 && c == '\0')
                        {
                            continue;
                        }
                        
                        fileName += c;
                    }

                    while (fileName.IndexOfAny(invalidChars) >= 0)
                    {
                        StringBuilder sb = new StringBuilder(fileName);
                        sb[fileName.IndexOfAny(invalidChars)] = '_';
                        fileName = sb.ToString();
                    }
                }
                else
                {
                    fileName = Path.GetFileNameWithoutExtension(file);
                }

                // Make sure we don't have duplicate file names in the same directory
                if (fileNames.Contains(fileName))
                {
                    int i = 0;
                    string newFileName;
                    
                    do
                    {
                        newFileName = fileName + " (" + ++i + ")";
                    } while (fileNames.Contains(newFileName));

                    fileName = newFileName;
                }

                fileNames.Add(fileName);
                fileName += ".txt";

                Console.Out.WriteLine("- " + fileName);

                string outPath = outDir.FullName;
                
                if (isMessage)
                {
                    outPath = Path.Combine(outPath, "messages");
                }

                string newFile = Path.Combine(outPath, fileName);
                
                if (File.Exists(newFile))
                {
                    File.Delete(newFile);
                }

                File.WriteAllText(newFile, text);
            }
        }
    }
}