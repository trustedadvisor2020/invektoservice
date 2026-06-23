using Hangfire;
using Hangfire.PostgreSql;
using Chatinbox.Shared.Constants;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// Note: ErrorCodes used for JobStorageConnectionFailed messaging.

namespace Chatinbox.Shared.Hosting;

/// <summary>
/// G7: Shared Hangfire setup for all microservices. Each service registers its own
/// server with a distinct queue name; all services share the same PostgreSQL storage
/// under schema <c>hangfire</c>. Queue-per-service topology keeps microservice
/// isolation intact while letting Hangfire's advisory-lock leader election
/// coordinate multi-instance deployments.
/// </summary>
public static class HangfireSetup
{
    /// <summary>Schema used by Hangfire.PostgreSql for its internal tables.</summary>
    public const string HangfireSchemaName = "hangfire";

    /// <summary>Default shutdown grace period (matches existing IHostedService behaviour).</summary>
    public static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers Hangfire services + a dedicated server for the calling microservice.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="queueName">Queue this service owns (e.g. <c>appointments</c>).</param>
    /// <param name="connectionString">PostgreSQL connection string (shared DB).</param>
    /// <param name="workerCount">Worker count; defaults to Hangfire's default (Environment.ProcessorCount * 5).</param>
    /// <param name="enableScheduler">
    /// When <c>false</c>, effectively disables the server's <c>RecurringJobScheduler</c> by
    /// pushing <see cref="BackgroundJobServerOptions.SchedulePollingInterval"/> far into the
    /// future. Use for worker servers that must NOT compete for the shared
    /// <c>recurring-jobs</c> advisory lock — only the leader (Backend) schedules. See §12.
    /// </param>
    public static IServiceCollection AddInvektoHangfire(
        this IServiceCollection services,
        string queueName,
        string connectionString,
        int? workerCount = null,
        bool enableScheduler = true)
    {
        if (string.IsNullOrWhiteSpace(queueName))
            throw new InvalidOperationException(
                "FATAL: AddInvektoHangfire called without a queue name — set Queues:Name in appsettings.json or pass explicitly (e.g. \"appointments\").");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"FATAL: Hangfire connection string is empty. Set ConnectionStrings:Hangfire or ConnectionStrings:PostgreSQL in appsettings.json. Error code [{ErrorCodes.JobStorageConnectionFailed}].");

        services.AddHangfire((_, config) =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = HangfireSchemaName,
                        PrepareSchemaIfNecessary = true,
                        QueuePollInterval = TimeSpan.FromSeconds(15),
                        InvisibilityTimeout = TimeSpan.FromMinutes(30),
                        DistributedLockTimeout = TimeSpan.FromMinutes(1)
                    });
        });

        services.AddHangfireServer(options =>
        {
            // ServerName intentionally NOT set — Hangfire defaults to
            // "<MachineName>:<ProcessId>" which is unique per instance and prevents
            // identity collision in shared PG storage during blue-green / scale-out
            // deployments. Setting a fixed name like "invekto-{queue}" would alias
            // multiple replicas to the same server row.
            options.Queues = new[] { queueName };
            options.ShutdownTimeout = DefaultShutdownTimeout;
            if (workerCount.HasValue && workerCount.Value > 0)
                options.WorkerCount = workerCount.Value;
            if (!enableScheduler)
            {
                // Follow-up #1: Worker servers must not race Backend for the shared
                // `recurring-jobs` advisory lock. Hangfire 1.8 has no direct toggle to
                // suppress RecurringJobScheduler; push polling far into the future so
                // a worker rarely competes for the lock. Race-safety note: even if a
                // worker outlives the polling delay, Hangfire's advisory-lock leader
                // election still serialises scheduling; Backend (the canonical leader,
                // enableScheduler=true) wins under normal ops. The interval is a
                // best-effort suppression, not a hard exclusivity guarantee.
                //
                // Boot-time crash (observed in production stderr 2026-05-06 for
                // Appointments / Automation / Integrations workers — see
                // arch/lessons-learned.md): Hangfire 1.8 setter at
                // BackgroundJobServerOptions.cs:123 threw ArgumentOutOfRangeException
                // for TimeSpan.FromMilliseconds(int.MaxValue) with message "must be
                // non-negative and either equal to or less than 2147483647 ms".
                // Empirical fix — reduced to TimeSpan.FromDays(20) (~1.73e9 ms, well
                // under the int32-ms cap). We make no claim about the setter's exact
                // boundary semantics beyond "int.MaxValue rejected, 20-day value
                // accepted across all worker services post-deploy".
                options.SchedulePollingInterval = TimeSpan.FromDays(20);
            }
        });

        return services;
    }

    /// <summary>
    /// Resolves the Hangfire connection string: prefers <c>ConnectionStrings:Hangfire</c>,
    /// falls back to <c>ConnectionStrings:PostgreSQL</c> (shared DB).
    /// </summary>
    public static string ResolveConnectionString(IConfiguration config)
    {
        var cs = config.GetConnectionString("Hangfire");
        if (!string.IsNullOrWhiteSpace(cs)) return cs;
        return config.GetConnectionString("PostgreSQL") ?? string.Empty;
    }

    /// <summary>
    /// Forces Hangfire's static <see cref="JobStorage.Current"/> to initialize from DI.
    /// Call this once after <c>app.Build()</c> and BEFORE any static
    /// <c>RecurringJob.AddOrUpdate</c> invocation. Without this, the static API throws
    /// "JobStorage instance has not been initialized yet" because Hangfire sets
    /// <see cref="JobStorage.Current"/> lazily via the DI factory.
    /// </summary>
    public static void EnsureJobStorageInitialized(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<JobStorage>();
    }
}
