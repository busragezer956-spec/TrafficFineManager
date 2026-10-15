using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrafficFineManager.Web.Data;
using Xunit;

namespace TrafficFineManager.Tests;

public class HttpTests
{
    private const string Password = "HttpTest!2026";
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var sql = Environment.GetEnvironmentVariable("TFM_TEST_SQLSERVER");
            var useSql = !string.IsNullOrWhiteSpace(sql);
            var connection = useSql ? new SqlConnectionStringBuilder(sql!) {
                InitialCatalog = "TrafficFineManager_HttpTests_" + Guid.NewGuid().ToString("N")
            }.ConnectionString : "UnusedForInMemoryTests";
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["Demo:Enabled"] = useSql.ToString(), ["Demo:Password"] = Password
            }));
            if (!useSql) builder.ConfigureTestServices(services => {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                var databaseName = "HttpTest-" + Guid.NewGuid();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }
        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TFM_TEST_SQLSERVER")))
            {
                using var scope = host.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
                Task.Run(() => DemoSeeder.SeedAsync(scope.ServiceProvider, host.Services.GetRequiredService<IConfiguration>())).GetAwaiter().GetResult();
            }
            return host;
        }
        public override async ValueTask DisposeAsync()
        {
            using (var scope = Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            await base.DisposeAsync();
        }
    }
    private static HttpClient Client(Factory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions {
        AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
    });
    private static async Task<string> Token(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, html);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static async Task Login(HttpClient client, string email)
    {
        var token = await Token(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Email"] = email, ["Password"] = Password, ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    [Fact] public async Task Anonymous_request_redirects_to_login()
    {
        await using var factory = new Factory(); using var client = Client(factory);
        var response = await client.GetAsync("/Fines");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }
    [Fact] public async Task Operator_can_render_all_primary_screens()
    {
        await using var factory = new Factory(); using var client = Client(factory);
        await Login(client, "operasyon@demo.local");
        foreach (var path in new[] { "/", "/Vehicles", "/Vehicles/Create", "/Fines", "/Fines/Create", "/Fines/Details/1", "/Fines/Edit/1", "/Fines?status=ManagerApproval", "/Fines?search=DEMO" })
        {
            var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, path + " returned " + response.StatusCode + " " + await response.Content.ReadAsStringAsync());
            var output = Environment.GetEnvironmentVariable("TFM_RENDER_DIR");
            if (!string.IsNullOrWhiteSpace(output)) {
                Directory.CreateDirectory(output);
                var name = path == "/" ? "dashboard" : Regex.Replace(path.Trim('/'), "[^a-zA-Z0-9]", "-");
                await File.WriteAllTextAsync(Path.Combine(output, name + ".html"), await response.Content.ReadAsStringAsync());
            }
        }
    }
    [Fact] public async Task Post_without_antiforgery_token_is_rejected()
    {
        await using var factory = new Factory(); using var client = Client(factory);
        await Login(client, "operasyon@demo.local");
        var response = await client.PostAsync("/Vehicles/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Plate"] = "34TEST001" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact] public async Task Finance_cannot_open_creation_endpoints()
    {
        await using var factory = new Factory(); using var client = Client(factory);
        await Login(client, "finans@demo.local");
        foreach (var path in new[] { "/Vehicles/Create", "/Fines/Create", "/Fines/Edit/1" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Denied", response.Headers.Location!.ToString());
        }
    }
    [Fact] public async Task Turkish_decimal_binding_and_persistence_work()
    {
        await using var factory = new Factory(); using var client = Client(factory);
        await Login(client, "operasyon@demo.local");
        var token = await Token(client, "/Fines/Create");
        var response = await client.PostAsync("/Fines/Create", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["VehicleId"] = "1", ["ReferenceNumber"] = "HTTP-001", ["ViolationDate"] = "2026-01-01",
            ["Amount"] = "1500,25", ["Description"] = "HTTP test kaydı", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fine = await db.TrafficFines.Include(x => x.History).SingleAsync(x => x.ReferenceNumber == "HTTP-001");
        Assert.Equal(1500.25m, fine.Amount); Assert.Single(fine.History);
    }
    [Fact] public async Task Foreign_login_return_url_is_ignored()
    {
        await using var factory = new Factory(); using var client = Client(factory);
        var token = await Token(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Email"] = "operasyon@demo.local", ["Password"] = Password,
            ["ReturnUrl"] = "https://example.com", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("example.com", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("yonetici@demo.local", "DEMO-2026-002", "DEMO-2026-003")]
    [InlineData("finans@demo.local", "DEMO-2026-003", "DEMO-2026-002")]
    public async Task My_queue_only_shows_current_users_approval_stage(string email, string expected, string excluded)
    {
        await using var factory = new Factory(); using var client = Client(factory);
        await Login(client, email);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Fines?mine=true"));
        Assert.Contains(expected, html); Assert.DoesNotContain(excluded, html);
        Assert.Contains("Onayımı bekleyenler", html);
    }
    [Fact] public async Task Operator_cannot_request_approval_queue()
    {
        await using var factory = new Factory(); using var client = Client(factory);
        await Login(client, "operasyon@demo.local");
        var response = await client.GetAsync("/Fines?mine=true");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Denied", response.Headers.Location!.ToString());
    }
}
