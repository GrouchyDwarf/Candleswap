using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;
using RedDuck.Candleswap.Candles.CSharp;
using WebSocket.Uniswap.Background;
using WebSocket.Uniswap.Middlewares;
using WebSocket.Uniswap.Services;
using static Domain.Types;

namespace WebSocket.Uniswap
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
                loggingBuilder.AddAzureWebAppDiagnostics();
            });

            services.AddControllers();

            services.AddWebSocketConnections();

            services.AddSingleton<IHostedService, HeartbeatService>();

            services.AddSingleton<ISqlConnectionProvider, SqlConnectionProvider>();
            services.AddTransient<ILogicService, LogicService>();
            services.AddSingleton<IWeb3>(new Web3("https://bsc-dataseed.binance.org/"));


            services.AddTransient<ICandleStorageService, CandleStorageService>();
            services.AddSingleton<IIndexerService, IndexerService>(sp => 
                new IndexerService(sp.GetService<IWeb3>(), sp.GetService<ISqlConnectionProvider>(),
                                   sp.GetService<ILogger<BlockchainListener>>()));

            services.AddSingleton<IDictionary<(Pair, int), CancellationTokenSource>>(
                _ => new Dictionary<(Pair, int), CancellationTokenSource>());

            services.AddHostedService<BlockchainListener>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebSocket.Uniswap", Version = "v1" });
            });

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebSocket.Uniswap v1"));

            WebSocketConnectionsOptions webSocketConnectionsOptions = new WebSocketConnectionsOptions
            {
                //AllowedOrigins = new HashSet<string> { "wss://localhost:5001/socket" },
                SendSegmentSize = 4 * 1024
            };

            app.UseWebSockets();

            app.MapWebSocketConnections("/socket", webSocketConnectionsOptions);

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
