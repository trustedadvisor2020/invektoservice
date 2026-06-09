using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Outbound.Data;
using Invekto.Outbound.Services;
using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace InvektoServis.Tests._Shared.Factories;

/// <summary>
/// FEAT-PROJELER send-exec SS-C test host. Builds on <see cref="OutboundTestFactory"/> but ENABLES the
/// Projects + BulkSend gates for the default test tenant and SUBSTITUTES ProjectsRepository +
/// BulkSendRepository, so the project run-dispatch endpoints can be exercised WITHOUT a live Postgres:
/// eligibility (HSM reject / no-content / no-targets) rejects before any DB call, and the valid preview
/// path resolves the content carrier (template_id vs inline) against the faked snapshot. Reject paths
/// never reach the (real, sealed) BulkSendOrchestrator.
///
/// Gate enablement is done by RE-REGISTERING the option singletons in ConfigureTestServices (which runs
/// AFTER the app's startup registration), NOT via ConfigureAppConfiguration: Program binds
/// BulkSendOptions/ProjectsOptions EAGERLY at startup, before the factory's in-memory config merges.
///
/// The faked repos are exposed via computed lookups against the built provider (the substitutes are
/// registered as singletons) so the factory needs no null-forgiving backing fields.
/// </summary>
public sealed class OutboundProjectsSendTestFactory : OutboundTestFactory
{
    public ProjectsRepository FakeProjectsRepo => Services.GetRequiredService<ProjectsRepository>();
    public BulkSendRepository FakeBulkRepo => Services.GetRequiredService<BulkSendRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            var fakeDb = new PostgresConnectionFactory("Host=localhost;Port=5432;Database=test;Username=test;Password=test");
            var fakeLogger = FakeLoggerFactory.Create("Outbound.ProjectsSend.Test");

            // Open both gates for the default test tenant (run dispatch is dual-gated). The product binds
            // these eagerly at startup, so swap the singletons here rather than via config.
            services.RemoveAll<ProjectsOptions>();
            services.AddSingleton(new ProjectsOptions { Enabled = true, AllowAllTenants = true });

            services.RemoveAll<BulkSendOptions>();
            services.AddSingleton(new BulkSendOptions
            {
                Enabled = true,
                AllowedTenantIds = new List<int> { TestJwtTokenHelper.DefaultTenantId },
                MaxRecipientsPerCampaign = 50,
                PreviewSampleSize = 10,
            });

            services.RemoveAll<ProjectsRepository>();
            services.AddSingleton(Substitute.For<ProjectsRepository>(fakeDb));

            services.RemoveAll<BulkSendRepository>();
            services.AddSingleton(Substitute.For<BulkSendRepository>(fakeDb, fakeLogger));
        });
    }
}
