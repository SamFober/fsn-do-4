using FrontEnd;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebApi.Interfaces.Repositories;
using WebApi.Repositories;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5213/") });
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();


await builder.Build().RunAsync();
