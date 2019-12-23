using Consul;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ConsulKeyValueAppender
{
    class Program
    {
        const string _defaultConsulAddr = "http://localhost:8500";
        const string _defaultConsulToken = "anonymous";


        static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "consulAppender";
            app.Description = "Consul Key/Value appender";
            app.HelpOption("-?|-h|--help");
            var consulAddrOption = app.Option($"-c|--consulAddress <{_defaultConsulAddr}>", "Consul HTTP address", CommandOptionType.SingleValue);
            var consulMasterTokenOption = app.Option($"-m|--masterToken <{_defaultConsulToken}>", "Master token value", CommandOptionType.SingleValue);
            app.OnExecute(async () =>
            {
                var client = new ConsulClient(configuration => 
                { 
                    configuration.Address = new Uri(consulAddrOption.HasValue() ? consulAddrOption.Value() : _defaultConsulAddr);
                    if (consulMasterTokenOption.HasValue()) configuration.Token = consulMasterTokenOption.Value();
                });
                var filesToProcess = Directory.GetFiles("data/", "*Data.json");
                foreach (var file in filesToProcess)
                {
                    var jsonBytes = File.ReadAllBytes(file);
                    if (jsonBytes.Length == 0) continue;

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonBytes);
                        var root = jsonDoc.RootElement;                        
                        foreach (var item in root.EnumerateArray())
                        {
                            var key = item.GetProperty("key").GetString();
                            var val = item.GetProperty("value").ToString();
                            var pair = new KVPair(key) { Value = Encoding.UTF8.GetBytes(val) };
                            await client.KV.Put(pair);
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }                    
                }

                Console.WriteLine("Finished uploading Consul configuration");
                return 0;
            });

            try
            {
                app.Execute(args);
            }
            catch (CommandParsingException ex)
            {                
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to execute application: {ex.Message}");
            }
        }

        private static object OnExecute()
        {
            throw new NotImplementedException();
        }
    }
}
