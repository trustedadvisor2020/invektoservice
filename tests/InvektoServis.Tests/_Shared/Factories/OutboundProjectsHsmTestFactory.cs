using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Outbound.Services;
using Invekto.Shared.Contracts.Inma;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvektoServis.Tests._Shared.Factories;

/// <summary>
/// FEAT-PROJELER / cxapi PR-4 test host. Builds on <see cref="OutboundProjectsSendTestFactory"/>
/// (Projects + BulkSend gates open, repos faked) and additionally:
///   (a) OPENS the CxapiSend allowlist for the default test tenant — so the HSM validation matrix
///       past the INV-OB-085 gate can be exercised (the gate itself is pinned in ProjectSendTests,
///       whose factory keeps CxapiSend at its prod default: disabled), and
///   (b) routes the (real, sealed) WapCrmTemplateClient through <see cref="TemplateCatalog"/> so each
///       test cans the live cxapi /api/templates response — catalog reachability, slug ownership and
///       requiredInputs coverage all run against the REAL parse path, no live vendor.
/// WapCRM creds come from the base factory's FakeRepo (OutboundRepository.GetWapCrmSettingsAsync).
/// </summary>
public sealed class OutboundProjectsHsmTestFactory : OutboundProjectsSendTestFactory
{
    public MockHttpMessageHandler TemplateCatalog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CxapiSendOptions>();
            services.AddSingleton(new CxapiSendOptions
            {
                Enabled = true,
                AllowedTenantIds = new List<int> { TestJwtTokenHelper.DefaultTenantId },
            });

            // Re-bind the typed template client's primary handler to the canned catalog. The client
            // itself stays REAL (sealed) so envelope parse + outcome classification are exercised.
            services.AddHttpClient<WapCrmTemplateClient>()
                .ConfigurePrimaryHttpMessageHandler(() => TemplateCatalog);
        });
    }
}
