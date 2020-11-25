using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

var factory = new ChuckNorrisFactFactory();
var context = factory.CreateDbContext();

List<ChuckNorrisFact> facts = await ProcessCommandArgs(args, context);
if (facts == null) return;

var dbFacts = await context.Facts.AsNoTracking().ToListAsync();
var allFactsAfterInsert = await ValidateFacts(dbFacts, facts);

context.Facts.AddRange(facts);
await context.SaveChangesAsync();

Console.WriteLine($"{facts.Count} Fact(s) inserted");

if (allFactsAfterInsert)
{
    Console.WriteLine("You got all Facts in your database");
}

async Task<List<ChuckNorrisFact>> ProcessCommandArgs(string[] args, ChuckNorrisFactContext context)
{
    if (args.Length > 0)
    {
        if (args[0].ToLower() is "clear")
        {
            await context.Database.ExecuteSqlRawAsync("DELETE from Facts");
            return null;
        }

        return await RetrieveFacts(int.Parse(args[0]));
    }
    else
    {
        return await RetrieveFacts();
    }
}

async Task<bool> ValidateFacts(List<ChuckNorrisFact> dbFacts, List<ChuckNorrisFact> localFacts)
{
    var retryCount = 0;

    foreach (var fact in localFacts)
    {
        if (dbFacts.Contains(fact) && retryCount < 10)
        {
            localFacts.Remove(fact);
            localFacts.Add((await RetrieveFacts(1))[0]);
            retryCount++;
        }
        else if (retryCount >= 10)
        {
            return true;
        }
    }

    return false;
}

async Task<List<ChuckNorrisFact>> RetrieveFacts(int count = 5)
{
    if (count > 10)
    {
        return null;
    }

    using HttpClient httpClient = new HttpClient();

    try
    {
        List<ChuckNorrisFact> retrievedFacts = new List<ChuckNorrisFact>();
        HttpResponseMessage response;
        string responseBody;
        ChuckNorrisFact deserializedFact;

        while (count-- != 0)
        {
            response = await httpClient.GetAsync("https://api.chucknorris.io/jokes/random?category!%3Dexplicit");
            response.EnsureSuccessStatusCode();
            responseBody = await response.Content.ReadAsStringAsync();

            deserializedFact = JsonSerializer.Deserialize<ChuckNorrisFact>(responseBody)!;

            if (!retrievedFacts.Contains(deserializedFact))
            {
                retrievedFacts.Add(deserializedFact);
            }
            else count++;
        }

        return retrievedFacts;
    }
    catch (HttpRequestException e)
    {
        Console.WriteLine(e.Message);
    }
    return null;
}

class ChuckNorrisFact
{
    public int Id { get; set; }

    [JsonPropertyName("id")]
    [MaxLength(40)]
    public string ChuckNorrisId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    [MaxLength(1024)]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Joke { get; set; } = string.Empty;
}

class ChuckNorrisFactContext : DbContext
{
    public DbSet<ChuckNorrisFact> Facts { get; set; }

    public ChuckNorrisFactContext(DbContextOptions<ChuckNorrisFactContext> options) : base(options)
    { }
}

class ChuckNorrisFactFactory : IDesignTimeDbContextFactory<ChuckNorrisFactContext>
{
    public ChuckNorrisFactContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<ChuckNorrisFactContext>();
        optionsBuilder.UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new ChuckNorrisFactContext(optionsBuilder.Options);
    }
}

