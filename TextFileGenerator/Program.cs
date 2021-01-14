using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextFileGenerator
{
    class Program
    {
        // Dumping at 800MB
        private const ulong MaxMbDump = 838900000UL;

        private static readonly ConcurrentBag<string> dataToDump = new ConcurrentBag<string>();

        private static readonly IReadOnlyCollection<char> availableChars = Array.AsReadOnly(new[]
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z'
        });

        static async Task Main(string[] args)
        {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var invalidPathChars = Path.GetInvalidPathChars();
            var directory = string.Empty;

            var directoryValid = false;
            while (!directoryValid)
            {
                Console.Write(
                    "Type in the directory where the file should be created (Leave blank for current directory): ");
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

            Console.Write("Type in the amount of threads the application should run on (blank for 1): ");
            var threadCount = 1U;
            var tempThreadCount = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(tempThreadCount) || !uint.TryParse(tempThreadCount, out threadCount))
            {
                Console.WriteLine("Invalid thread count input");
            }
            else if (threadCount > Environment.ProcessorCount)
            {
                threadCount = (uint) Environment.ProcessorCount;
                Console.WriteLine($"Too many threads specified, using {threadCount} threads");
            }

            Console.WriteLine($"Running on {threadCount} threads");

            var fileName = string.Empty;
            var fileSize = 0UL;

            var exitText = string.Empty;
            while (!exitText.Equals("quit", StringComparison.InvariantCultureIgnoreCase) &&
                   !exitText.Equals("exit", StringComparison.InvariantCultureIgnoreCase))
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
                    Console.Write("Type in the file size in MegaBytes: ");
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
                    Console.Write($"Type in the file size in MegaBytes (Leave blank to use {fileSize}): ");
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

                if (threadCount > 1)
                {
                    await GenerateFileMultiThread(directory, fileName, fileSize * 1024 * 1024, threadCount);
                }
                else
                {
                    await GenerateFileSingleThread(directory, fileName, fileSize * 1024 * 1024);
                }

                Console.Write("Would you like to quit? (exit/quit/blank)");
                exitText = Console.ReadLine();
            }
        }

        private static string RemoveInvalidChars(string text, char[] invalidFileNameChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (text.Any(invalidFileNameChars.Contains))
            {
                for (var i = text.Length - 1; i >= 0; i--)
                {
                    var currentChar = text[i];
                    if (invalidFileNameChars.Contains(currentChar))
                    {
                        text = text.Substring(0, i) + text.Substring(i + 1);
                    }
                }
            }

            return text;
        }

        private static async Task GenerateFileSingleThread(string path, string fileName, ulong byteSize)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var fullPath = Path.Combine(path, fileName + ".txt");
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                File.Create(fullPath).Close();

                await using var sw = new StreamWriter(fullPath, false, Encoding.UTF8, 83890000);

                // Dump on 1GB
                // var actualDumpInterval = 1074000000000;
                var actualDumpInterval = 838900000UL;
                var currentDumpInterval = actualDumpInterval;
                Console.WriteLine($"Dump interval: {(currentDumpInterval / 1024 / 1024):F}MB");

                var stopWatch = new Stopwatch();
                var textSb = new StringBuilder();
                stopWatch.Start();
                var dumpCount = 1UL;
                var dataGenStopwatch = new Stopwatch();
                dataGenStopwatch.Start();
                for (ulong i = 0; i < byteSize; i++)
                {
                    textSb.Append('a');

                    if (i != currentDumpInterval)
                    {
                        continue;
                    }

                    dataGenStopwatch.Stop();
                    Console.WriteLine($"DataGen time: {(dataGenStopwatch.ElapsedMilliseconds / 1000d):F}");
                    dataGenStopwatch.Reset();

                    var fileWriteStopwatch = new Stopwatch();
                    dumpCount++;
                    currentDumpInterval = actualDumpInterval * dumpCount;
                    Console.WriteLine(
                        $"Dumping data at {(i * 100.0d / byteSize):F}%. Generated ({(i / 1024 / 1024):F} MB in {(stopWatch.ElapsedMilliseconds / 1000d):F}S). Dumping again at: {(currentDumpInterval / 1024 / 1024):F}MB");
                    fileWriteStopwatch.Start();
                    await sw.WriteAsync(textSb.ToString());
                    // File writing is slow, look at https://www.jeremyshanks.com/fastest-way-to-write-text-files-to-disk-in-c/
                    // File writing is slow, look at https://stackoverflow.com/questions/955911/how-to-write-super-fast-file-streaming-code-in-c
                    fileWriteStopwatch.Stop();
                    Console.WriteLine($"FileWrite time: {(fileWriteStopwatch.ElapsedMilliseconds / 1000d):F}");
                    textSb.Clear();

                    dataGenStopwatch.Start();
                }

                var finalText = textSb.ToString();
                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    await sw.WriteAsync(textSb.ToString());
                }

                stopWatch.Stop();
                Console.WriteLine($"File written in ({(stopWatch.ElapsedMilliseconds / 1000d):F}S) to : {fullPath}");
            }
            catch (OutOfMemoryException oom)
            {
                Console.WriteLine($"Out of memory:( : {oom}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e}");
            }
            finally
            {
                Console.Read();
            }
        }

        private static async Task GenerateFileMultiThread(string path, string fileName, ulong byteSize,
            uint threadCount)
        {
            var stopwatch = new Stopwatch();
            var dataPerThread = byteSize / threadCount;
            if (dataPerThread > MaxMbDump)
            {
                dataPerThread = MaxMbDump;
                threadCount = (uint) (byteSize / dataPerThread);
                Console.WriteLine($"New threadCount: {threadCount}");
            }

            var totalAfterThreads = dataPerThread * threadCount;
            var lastThreadDataCount = dataPerThread;

            if (totalAfterThreads != byteSize)
            {
                lastThreadDataCount = byteSize - totalAfterThreads;
            }

            stopwatch.Start();
            for (var i = 0; i < threadCount; i++)
            {
                var dataCount = i == threadCount ? lastThreadDataCount : dataPerThread;
                Task.Factory.StartNew(() =>
                {
                    var threadSw = new Stopwatch();
                    var textSb = new StringBuilder();
                    threadSw.Start();
                    for (var j = 0UL; j < dataCount; j++)
                    {
                        textSb.Append('a');
                    }

                    dataToDump.Add(textSb.ToString());
                    threadSw.Stop();
                    Console.WriteLine($"Thread {i} finished in {threadSw.ElapsedMilliseconds}MS");
                });
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var fullPath = Path.Combine(path, fileName + ".txt");
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            File.Create(fullPath).Close();

            await using var sw = new StreamWriter(fullPath, false, Encoding.UTF8, 83890000);
            
            try
            {
                var writeCount = 0;

                while (writeCount < threadCount)
                {
                    if (dataToDump.IsEmpty || !dataToDump.TryTake(out var dataDump))
                    {
                        continue;
                    }

                    var writeSw = new Stopwatch();

                    writeCount++;
                    writeSw.Start();
                    await sw.WriteAsync(dataDump);
                    writeSw.Stop();
                    Console.WriteLine($"Wrote data in {writeSw.ElapsedMilliseconds}MS");
                    await sw.FlushAsync();
                }

                stopwatch.Stop();
                Console.WriteLine($"Thread wrote {byteSize} bytes to {fullPath} in {stopwatch.ElapsedMilliseconds}MS");
            }
            catch (OutOfMemoryException oom)
            {
                Console.WriteLine($"Out of memory:( : {oom}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e}");
            }
            finally
            {
                dataToDump.Clear();
                sw.Close();
                Console.Read();
            }
        }

        private static async Task AppendToFile(string fullPath, string data)
        {
            // TODO : Move this to outside the loop to not create it every time
            await using var fileStream = File.AppendText(fullPath);
            await fileStream.WriteAsync(data);
            await fileStream.FlushAsync();
            fileStream.Close();
        }
    }
}