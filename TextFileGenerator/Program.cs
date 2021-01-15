using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextFileGenerator
{
    internal static class Program
    {
        private static readonly char[] AvailableChars =
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z'
        };

        private static string _randomData = string.Empty;

        private static async Task Main(string[] args)
        {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var invalidPathChars = Path.GetInvalidPathChars();
            var directory = string.Empty;

            var directoryValid = false;
            while (!directoryValid)
            {
                Console.Write("Type in the directory where the file should be created (Leave blank for current directory): ");
                var tempDirectoryName = RemoveInvalidChars(Console.ReadLine(), invalidPathChars);
                if (string.IsNullOrWhiteSpace(tempDirectoryName))
                {
                    directory = Directory.GetCurrentDirectory();
                    directoryValid = true;
                }
                else if (Directory.Exists(tempDirectoryName))
                {
                    directoryValid = true;
                    directory = tempDirectoryName;
                }
                else
                {
                    Console.WriteLine("Directory does not exist, please create it and try again.");
                }
            }

            var fileName = string.Empty;
            var fileSize = 0UL;

            var exitText = string.Empty;
            while (!exitText.Contains("q", StringComparison.InvariantCultureIgnoreCase) &&
                   !exitText.Contains("e", StringComparison.InvariantCultureIgnoreCase))
            {
                var fileNameValid = false;
                var fileNameQuestionText = string.IsNullOrWhiteSpace(fileName)
                    ? "Type in the file name without the extension: "
                    : $"Type in the file name without the extension (Leave blank to use \"{fileName}\"): ";
                while (!fileNameValid)
                {
                    Console.Write(fileNameQuestionText);
                    var tempFileName = RemoveInvalidChars(Console.ReadLine(), invalidFileNameChars);

                    if (string.IsNullOrWhiteSpace(tempFileName))
                    {
                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            fileNameValid = true;
                            continue;
                        }

                        Console.WriteLine("Invalid file name.");
                    }
                    else
                    {
                        fileName = tempFileName;
                        fileNameValid = true;
                    }
                }

                if (fileSize == 0)
                {
                    Console.Write("Type in the minimum file size in GigaBytes (no decimals): ");
                    var fileSizeValid = false;
                    while (!fileSizeValid)
                    {
                        var tempFileSize = Console.ReadLine();
                        if (ulong.TryParse(tempFileSize, out fileSize))
                        {
                            fileSizeValid = true;
                        }
                        else
                        {
                            Console.Write("File size invalid, please type in an integer number: ");
                        }
                    }
                }
                else
                {
                    Console.Write($"Type in the file size in GigaBytes (no decimals) (Leave blank to use {fileSize}): ");
                    var fileSizeValid = false;
                    while (!fileSizeValid)
                    {
                        var tempFileSize = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(tempFileSize))
                        {
                            fileSizeValid = true;
                        }
                        else if (ulong.TryParse(tempFileSize, out fileSize))
                        {
                            fileSizeValid = true;
                        }
                        else
                        {
                            Console.Write("File size invalid, please type in an integer number: ");
                        }
                    }
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var fullPath = Path.Combine(directory, fileName + ".txt");
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                File.Create(fullPath).Close();

                await GenerateFile(fullPath, fileSize);

                GC.Collect();

                Console.Write("Would you like to quit? (exit/quit/blank for no)");
                exitText = Console.ReadLine();
            }
        }

        private static string RemoveInvalidChars(string text, char[] invalidFileNameChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (!text.Any(invalidFileNameChars.Contains))
            {
                return text;
            }

            for (var i = text.Length - 1; i >= 0; i--)
            {
                var currentChar = text[i];
                if (invalidFileNameChars.Contains(currentChar))
                {
                    text = text.Substring(0, i) + text.Substring(i + 1);
                }
            }

            return text;
        }

        private static async Task GenerateFile(string fullPath, ulong gigaByteSize)
        {
            try
            {
                var finalByteSize = GetBytesFromGigaBytes(gigaByteSize);

                const ulong maxDumpMb = 999UL;
                var dumpIntervalDataCount = GetBytesFromMegaBytes(maxDumpMb);

                var stopWatch = new Stopwatch();
                stopWatch.Start();

                var dumpCount = finalByteSize / dumpIntervalDataCount;
                var allDumpsDataCount = dumpCount * dumpIntervalDataCount;
                var lastDumpDataCount = finalByteSize - allDumpsDataCount;

                var dataGenSw = new Stopwatch();

                string randomData;

                if (string.IsNullOrWhiteSpace(_randomData))
                {
                    randomData = GenerateRandomData(dumpIntervalDataCount, dataGenSw);
                    _randomData = randomData;
                }
                else
                {
                    // Cheating by using already generated data to write multiple times for performance reasons.
                    // It will still look completely random even though it's a repeat of 800MB of randomised characters
                    randomData = _randomData;
                }

                await using var streamWriter = new StreamWriter(fullPath, false, Encoding.UTF8, 80000000);
                for (ulong i = 0; i <= dumpCount; i++)
                {
                    if (i != dumpCount)
                    {
                        await WriteToFile(streamWriter, randomData);
                        await streamWriter.FlushAsync();
                        continue;
                    }

                    var lastWriteData = randomData.Substring(0, (int) lastDumpDataCount);

                    await WriteToFile(streamWriter, lastWriteData);
                    await streamWriter.FlushAsync();
                }

                streamWriter.Close();
                stopWatch.Stop();
                Console.WriteLine($"{gigaByteSize}GB written in ({stopWatch.ElapsedMilliseconds / 1000d:F}S) to : {fullPath}");
            }
            catch (OutOfMemoryException oom)
            {
                Console.WriteLine($"Out of memory:( : {oom}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e}");
            }
        }

        private static string GenerateRandomData(ulong dataCount, Stopwatch stopwatch)
        {
            Console.WriteLine("Data generation started");
            var random = new Random(Guid.NewGuid().GetHashCode());
            var textSb = new StringBuilder();
            stopwatch.Start();
            for (var i = 0UL; i < dataCount; i++)
            {
                var randomChar = AvailableChars[random.Next(0, AvailableChars.Length - 1)];
                textSb.Append(randomChar);
            }

            stopwatch.Stop();
            Console.WriteLine($"Data generation finished in {stopwatch.ElapsedMilliseconds / 1000d:F}S");
            return textSb.ToString();
        }

        private static async Task WriteToFile(TextWriter textWriter, string dataDump)
        {
            var fileWriteStopwatch = new Stopwatch();
            fileWriteStopwatch.Start();
            await textWriter.WriteAsync(dataDump);
            // File writing is sort of slow, look at https://www.jeremyshanks.com/fastest-way-to-write-text-files-to-disk-in-c/
            // File writing is sort of slow, look at https://stackoverflow.com/questions/955911/how-to-write-super-fast-file-streaming-code-in-c
            fileWriteStopwatch.Stop();
            Console.WriteLine($"Wrote {GetMegaBytesFromBytes((ulong)dataDump.Length)}MB to file in {fileWriteStopwatch.ElapsedMilliseconds / 1000d:F}S");
        }

        private static ulong GetBytesFromMegaBytes(ulong bytes)
        {
            return bytes * 1024 * 1024;
        }

        private static ulong GetMegaBytesFromBytes(ulong megabytes)
        {
            return megabytes / 1024 / 1024;
        }

        private static ulong GetBytesFromGigaBytes(ulong bytes)
        {
            return bytes * 1024 * 1024 * 1024;
        }
    }
}