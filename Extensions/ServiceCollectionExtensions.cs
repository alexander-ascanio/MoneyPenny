using System.Net.Http.Headers;
using MoneyPenny.Data;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag;
using MoneyPenny.Services.Rag.Export;
using MoneyPenny.Services.Rag.Validation;
using MoneyPenny.Services.Rag.Embeddings;
using MoneyPenny.Services.Rag.Generation;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Rag.Pricing;
using MoneyPenny.Services.Rag.Retrieval;
using MoneyPenny.Services.Cv;
using MoneyPenny.Services.Ocr;
using MoneyPenny.Services.Tickets;
using MoneyPenny.Services.TeamSupport;
using Microsoft.EntityFrameworkCore;

namespace MoneyPenny.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMoneyPennyDatabases(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ApplicationDatabaseOptions>(
            configuration.GetSection(ApplicationDatabaseOptions.SectionName));
        services.Configure<TicketsDatabaseOptions>(
            configuration.GetSection(TicketsDatabaseOptions.SectionName));
        services.Configure<VectorDatabaseOptions>(
            configuration.GetSection(VectorDatabaseOptions.SectionName));
        services.Configure<RagOptions>(
            configuration.GetSection(RagOptions.SectionName));
        services.Configure<TeamSupportApiOptions>(
            configuration.GetSection(TeamSupportApiOptions.SectionName));
        services.Configure<TesseractOptions>(
            configuration.GetSection(TesseractOptions.SectionName));

        var appDb = configuration.GetSection(ApplicationDatabaseOptions.SectionName).Get<ApplicationDatabaseOptions>()
            ?? new ApplicationDatabaseOptions();
        var ticketsDb = configuration.GetSection(TicketsDatabaseOptions.SectionName).Get<TicketsDatabaseOptions>()
            ?? new TicketsDatabaseOptions();
        var vectorDb = configuration.GetSection(VectorDatabaseOptions.SectionName).Get<VectorDatabaseOptions>()
            ?? new VectorDatabaseOptions();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(PostgresConnectionHelper.BuildConnectionString(appDb)));

        services.AddDbContext<TicketsDbContext>(options =>
            options.UseNpgsql(PostgresConnectionHelper.BuildConnectionString(ticketsDb)));

        services.AddDbContext<VectorDbContext>(options =>
            options.UseNpgsql(
                PostgresConnectionHelper.BuildConnectionString(vectorDb),
                npgsql => npgsql.UseVector()));

        return services;
    }

    public static IServiceCollection AddMoneyPennyServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenAiHttpClient(configuration);

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IVectorRepository, VectorRepository>();
        services.AddScoped<ICommentImageTextCacheRepository, CommentImageTextCacheRepository>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ITeamSupportAttachmentService, TeamSupportAttachmentService>();
        services.AddScoped<ITeamSupportTicketApiClient, TeamSupportTicketApiClient>();
        services.AddScoped<ITeamSupportActionApiClient, TeamSupportActionApiClient>();
        services.AddScoped<IRatedTicketsExportService, RatedTicketsExportService>();
        services.AddSingleton<ITesseractOcrService, TesseractOcrService>();
        services.AddScoped<ICommentImageOcrService, CommentImageOcrService>();
        services.AddScoped<IMessageBoxDetectionService, MessageBoxDetectionService>();
        services.AddScoped<ICommentImageMessageBoxService, CommentImageMessageBoxService>();
        services.AddScoped<IChunkingService, ChunkingService>();
        services.AddScoped<IImageTextExtractionService, OpenAiImageTextExtractionService>();
        services.AddScoped<ICommentContentService, CommentContentService>();
        services.AddScoped<IRagTokenEstimateService, RagTokenEstimateService>();
        services.AddScoped<ITicketIngestionService, TicketIngestionService>();
        services.AddScoped<IFirstCommentIndexService, FirstCommentIndexService>();
        services.AddSingleton<IFirstCommentBulkIndexJobStore, FirstCommentBulkIndexJobStore>();
        services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
        services.AddScoped<IRetrievalService, PgVectorRetrievalService>();
        services.AddScoped<IGenerationService, OpenAiGenerationService>();
        services.AddScoped<IRagOrchestrator, RagOrchestrator>();
        services.AddScoped<IResponseGroundingChecker, ResponseGroundingChecker>();
        services.AddSingleton<IRagAskResultCache, RagAskResultCache>();

        return services;
    }

    private static void AddOpenAiHttpClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(OpenAiEmbeddingService.HttpClientName, (sp, client) =>
        {
            var section = configuration.GetSection("ExternalApis:OpenAI");
            var baseUrl = section["BaseUrl"] ?? "https://api.openai.com/v1";
            var apiKey = section["ApiKey"] ?? string.Empty;
            var timeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeout) ? timeout : 60;

            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        services.AddHttpClient(OpenAiImageTextExtractionService.ImageDownloadHttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MoneyPenny/1.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        services.AddHttpClient(TeamSupportAttachmentService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MoneyPenny/1.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
    }
}
