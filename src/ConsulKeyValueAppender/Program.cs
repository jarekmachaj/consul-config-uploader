using Consul;
using Microsoft.Extensions.CommandLineUtils;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ConsulKeyValueAppender
{
    public class ConsulPolicy
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Rules 
        { 
            get 
            {
                return string.Join("\n", _rules);
            }
            set => _rules.Add(value);
        }

        private List<string> _rules = new List<string>();
        public void SetRule(string ruleName, string rulePrefix, string policy)
        {
            _rules.Add($"{ruleName} \"{rulePrefix}\" {{\n  policy = \"{policy}\"\n}}");
        }
    }

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
                var consulAddress = consulAddrOption.HasValue() ? consulAddrOption.Value() : _defaultConsulAddr;
                var consulMasterToken = consulMasterTokenOption.Value();

                var client = new ConsulClient(configuration => 
                { 
                    configuration.Address = new Uri(consulAddress);
                    if (string.IsNullOrWhiteSpace(consulMasterToken) == false) configuration.Token = consulMasterToken;
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
                
                var restClient = new RestClient($"{consulAddress}/v1");
                restClient.AddDefaultHeader("X-Consul-Token", "b1c89741-57d9-4e2c-98e8-7a71e9ae6b95");
                var restRequest = new RestRequest("/acl/policies", Method.GET);
                var response = restClient.Execute(restRequest);
                Console.WriteLine(response.Content);
                var plicyget = new RestRequest("/acl/policy/24ca309b-4909-27ec-3722-460c22c58e2b", Method.GET);
                var policyresp = restClient.Execute(plicyget);
                var policyPut = new RestRequest("/acl/policy", Method.PUT);
                var policyToPut = new ConsulPolicy() { Name = "read-configurationTest", Description = "Grants read access to all key/value information" };
                policyToPut.SetRule("key-prefix", "", "read");
                policyToPut.SetRule("key-prefix", "apps/jpc", "write");
                policyPut.AddJsonBody(policyToPut);
                var policyPutResp = restClient.Execute(policyPut);
                Console.WriteLine(policyPutResp.Content);
                Console.WriteLine(policyresp.Content);
                var content = response.Content;
                var acl = client.ACL.List().Result;// ACL.List().Result;
                var keys = client.KV.Keys("").Result;

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
