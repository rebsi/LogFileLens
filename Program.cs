using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;

namespace LogFileLense
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Required argument missing: path");
            }

            LogFileLense logFileLense = new LogFileLense();
            logFileLense.HandleFile(args[0]);
        }
    }

    public class LogFileLense
    {
        private readonly FixedSizedQueue<string> _buffer;
        private readonly IList<Regex> _filterRegexes = new List<Regex>();
        private readonly IDictionary<string, int> _filterResults = new Dictionary<string, int>();
        private readonly int _lenseSize;

        public LogFileLense()
        {
            if (!int.TryParse(ConfigurationManager.AppSettings["LenseSize"], out _lenseSize))
            {
                _lenseSize = 0;
                Console.Error.WriteLine($"Failed to parse App.config 'LenseSize'. Using default value: {_lenseSize}");
            }

            string rawFilterRegexes = ConfigurationManager.AppSettings["FilterRegexes"];
            if (string.IsNullOrWhiteSpace(rawFilterRegexes))
            {
                throw new Exception("Missing App.config setting: FilterRegexes");
            }

            foreach (string rawFilterRegex in rawFilterRegexes.Split(';'))
            {
                _filterRegexes.Add(new Regex(rawFilterRegex));
            }

            foreach (Regex filterRegex in _filterRegexes)
            {
                _filterResults.Add(filterRegex.ToString(), 0);
            }

            _buffer = new FixedSizedQueue<string>(_lenseSize);
        }

        public void HandleFile(string path)
        {
            int fixedPrintCnt = 0;
            _buffer.Clear();

            int lineNr = 0;

            using (StreamReader file = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    lineNr++;

                    LenseMatch lenseMatch = IsMatch(line);
                    if (lenseMatch != null)
                    {
                        if (_buffer.Count >= _lenseSize)
                        {
                            PrintLine();
                        }

                        PrintAndClearBuffer(lineNr);
                        PrintLine(line, lineNr, lenseMatch);
                        fixedPrintCnt = _lenseSize;
                    }
                    else
                    {
                        if (fixedPrintCnt > 0)
                        {
                            --fixedPrintCnt;
                            PrintLine(line, lineNr);
                        }
                        else
                        {
                            _buffer.Enqueue(line);
                        }
                    }
                }
            }

            PrintResults(lineNr);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Press Any Key to Continue");
            Console.ReadKey();
        }

        private void PrintLine(string line = "", int? lineNr = null, LenseMatch lenseMatch = null)
        {
            if (lenseMatch != null)
            {
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.Write("!");
                Console.ResetColor();
            }
            else
            {
                Console.Out.Write(" ");
            }

            if (lineNr != null)
            {
                Console.Out.Write($"{lineNr.Value,4}: ");
            }

            if (lenseMatch != null)
            {
                Console.Out.Write(line.Substring(0, lenseMatch.Position));

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.Write(line.Substring(lenseMatch.Position, lenseMatch.Length));
                Console.ResetColor();

                Console.Out.WriteLine(line.Substring(lenseMatch.Position + lenseMatch.Length));
            }
            else
            {
                Console.Out.WriteLine(line);
            }
        }

        private void PrintAndClearBuffer(int lineNr)
        {
            int cnt = _buffer.Count;
            while (cnt > 0)
            {
                PrintLine(_buffer.Dequeue(), lineNr - cnt);
                cnt = _buffer.Count;
            }
        }

        private LenseMatch IsMatch(string line)
        {
            foreach (Regex filterRegex in _filterRegexes)
            {
                Match m = filterRegex.Match(line);
                if (m.Success)
                {
                    _filterResults[filterRegex.ToString()] += 1;
                    return new LenseMatch(m.Index, m.Length);
                }
            }

            return null;
        }

        private void PrintResults(int lineNr)
        {
            PrintLine();
            PrintLine();
            PrintLine($"LenseMatch counts from {lineNr} checked lines:");
            foreach (KeyValuePair<string, int> filterResult in _filterResults)
            {
                PrintLine($"  {filterResult.Key}: {filterResult.Value}");
            }
        }
    }

    internal class LenseMatch
    {
        public LenseMatch(int position, int length)
        {
            Position = position;
            Length = length;
        }

        public int Position { get; }
        public int Length { get; }
    }

    internal class FixedSizedQueue<T>
    {
        private readonly Queue<T> _queue = new Queue<T>();

        public FixedSizedQueue(int limit)
        {
            Limit = limit;
        }

        public int Limit { get; }

        public int Count => _queue.Count;

        public void Enqueue(T obj)
        {
            _queue.Enqueue(obj);
            while (_queue.Count > Limit)
            {
                _queue.Dequeue();
            }
        }

        public T Dequeue()
        {
            return _queue.Dequeue();
        }

        public void Clear()
        {
            _queue.Clear();
        }
    }
}