using FrontEnd;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var webApiBaseUrl = Environment.GetEnvironmentVariable("WebApiBaseUrl") ?? "http://localhost:5001";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(webApiBaseUrl) });

await builder.Build().RunAsync();
