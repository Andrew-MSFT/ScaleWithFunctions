﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace LoadRunner
{
    class Program
    {
        const int NumberOfRequests = 50;
        const int SimultaneousRequests = 1;
        const int WarmUpRequestCount = SimultaneousRequests;
        const int Min = 1000000;
        const int Max = 2000000;
        const bool UseFunction = true;
        const string UrlBase = "http://localhost:56053/";
        const string ApiUrl = UrlBase + "api/Primes?min={0}&max={1}&useFunc={2}";
        const string OutputFile = "results-{0}-{1}.csv";
        //static Stopwatch _sw;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Warming up");
            var warmup = new RequestCounter(SimultaneousRequests, ApiUrl, 0, 10000, UseFunction, false);
            await warmup.MakeRequests(WarmUpRequestCount);

            Console.WriteLine("Warmup completed, starting test");

            var requests = new RequestCounter(SimultaneousRequests, ApiUrl, Min, Max, UseFunction, true);
            var task = requests.MakeRequests(NumberOfRequests);
            var mainPage = new RequestCounter(UrlBase, false);
            var mainPageTask = mainPage.MakeRecurringRequest(100, -1);
            await task;

            mainPage.Stop();
            await mainPageTask;

            double requestAvg = requests.TotalTime / NumberOfRequests;
            Console.WriteLine($"Successfully completed {requests.SuccessfulRequests}/{NumberOfRequests} requests in {requests.RequestStopWatch.ElapsedMilliseconds}, avg/request: {requestAvg}");

            double mainPageAvg = mainPage.TotalTime / mainPage.SuccessfulRequests;
            Console.WriteLine($"Main page was loaded {mainPage.SuccessfulRequests} with an average load time of {mainPageAvg}");

            var timestamp = DateTime.Now.TimeOfDay.ToString().Replace(":", "-");
            var outputFile = UseFunction ? string.Format(OutputFile, "function", timestamp) : string.Format(OutputFile, "local", timestamp);
            await PrintResults(outputFile, new List<RequestCounter>() { requests, mainPage });

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        static async Task PrintResults(string outputFile, List<RequestCounter> counters)
        {
            using (var writer = new StreamWriter(outputFile))
            {
                foreach(var counter in counters)
                {
                    if (counter.CounterType == RequestCounterTypes.Load)
                    {
                        await writer.WriteLineAsync($"Start,{counter.LoadStart}");
                        await writer.WriteLineAsync($"End,{counter.LoadEnd}");
                        await writer.WriteLineAsync();
                    }
                }
                await writer.WriteAsync("Time");
                for (int i = 0; i < counters.Count; i++)
                {
                    await writer.WriteAsync("," + counters[i].Url);
                }
                await writer.WriteLineAsync();

                for (int i = 0; i < counters.Count; i++)
                {
                    var padding = new string(',', i + 1);

                    foreach (var record in counters[i].RequestHistory)
                    {
                        await writer.WriteLineAsync(record.TimeStamp + padding + record.Duration);
                    }
                }
            }
        }
    }
}
