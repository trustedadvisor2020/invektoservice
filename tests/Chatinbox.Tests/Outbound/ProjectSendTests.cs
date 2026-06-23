using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Outbound.Data;
using Chatinbox.Shared.DTOs.Outbound;
using NSubstitute;

namespace Chatinbox.Tests.Outbound;

/// <summary>
/// FEAT-PROJELER send-exec SS-C — project RUN dispatch (Gönder). These exercise the eligibility gate and
/// the content-carrier routing (template vs inline) WITHOUT a live Postgres: ProjectsRepository +
/// BulkSendRepository are faked (see <see cref="OutboundProjectsSendTestFactory"/>), so reject paths
/// resolve before any DB call and the valid preview asserts which carrier the snapshot receives. The
/// snapshot SQL itself (multi-list DISTINCT dedup) + the live recompute are DB-backed and verified
/// against the running Postgres in the deploy/CI environment.
/// </summary>
public class ProjectSendTests : IClassFixture<OutboundProjectsSendTestFactory>
{
    private readonly OutboundProjectsSendTestFactory _factory;
    private readonly HttpClient _client;

    public ProjectSendTests(OutboundProjectsSendTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    private static ProjectDetail Detail(long id, string? kind, string? contentMode, int? outboundTemplateId, string? body, bool withTarget)
        => new()
        {
            Project = new ProjectSummary
            {
                Id = id,
                Name = "Test Proje",
                Status = "draft",
                TargetCount = withTarget ? 1 : 0,
                TemplateKind = kind,
                InstanceId = 555,
                ContentMode = contentMode,
                OutboundTemplateId = outboundTemplateId,
                PlainTextBody = body,
            },
            Targets = withTarget
                ? new List<ProjectTargetDto> { new() { DataListId = 10, ListName = "Liste", TotalRecords = 5, SendableCount = 3, SortOrder = 0 } }
                : new List<ProjectTargetDto>(),
        };

    [Fact]
    public async Task PreviewSend_WithoutAuth_Returns401()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.PostAsJsonAsync("/api/v1/projects/7/send/preview", new { campaign_id = "c1" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PreviewSend_HsmProject_NotAllowlisted_Rejected_Inv085_NoVendorCall()
    {
        // PR-4 P0-3 inertness pin: this factory leaves CxapiSend at its DEFAULT (disabled, empty
        // allowlist) — exactly the prod state. A fully-configured HSM project must reject with
        // INV-OB-085 BEFORE any vendor call or snapshot. (The allowlisted/validation matrix lives
        // in ProjectsServiceHsmTests with its own CxapiSend-enabled factory.)
        var detail = Detail(7, ProjectTemplateKinds.WapcrmTemplate, null, null, null, withTarget: true);
        detail.Project.WaTemplateId = "kampanya_tr";
        _factory.FakeProjectsRepo.GetAsync(Arg.Any<int>(), Arg.Is<long>(7), Arg.Any<CancellationToken>())
            .Returns(detail);
        // The FakeBulkRepo is a shared IClassFixture mock; clear prior tests' calls so the
        // "no snapshot for HSM" assertion below is order-independent.
        _factory.FakeBulkRepo.ClearReceivedCalls();

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/7/send/preview", new { campaign_id = "c1" });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-085");
        // The gate fires before any snapshot — the bulk repo is never touched.
        await _factory.FakeBulkRepo.DidNotReceive().CreatePreviewJobFromProjectAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<long[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<BulkSendRepository.HsmSnapshotInput?>());
    }

    [Fact]
    public async Task PreviewSend_HsmProject_WithoutTemplate_Rejected_Inv073()
    {
        // An HSM project with NO template selected fails config-resolution (before the allowlist
        // even matters this is a 4xx config error, mirrored by the SPA's sendDisabledReason).
        _factory.FakeProjectsRepo.GetAsync(Arg.Any<int>(), Arg.Is<long>(13), Arg.Any<CancellationToken>())
            .Returns(Detail(13, ProjectTemplateKinds.WapcrmTemplate, null, null, null, withTarget: true));

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/13/send/preview", new { campaign_id = "c6" });

        ((int)resp.StatusCode).Should().Be(400);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-073");
    }

    [Fact]
    public async Task PreviewSend_PlainTextNoContent_Rejected_Inv074()
    {
        _factory.FakeProjectsRepo.GetAsync(Arg.Any<int>(), Arg.Is<long>(8), Arg.Any<CancellationToken>())
            .Returns(Detail(8, ProjectTemplateKinds.PlainText, contentMode: null, null, null, withTarget: true));

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/8/send/preview", new { campaign_id = "c2" });

        resp.StatusCode.Should().Be((HttpStatusCode)422);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-074");
    }

    [Fact]
    public async Task PreviewSend_NoTargets_Rejected_Inv075()
    {
        _factory.FakeProjectsRepo.GetAsync(Arg.Any<int>(), Arg.Is<long>(9), Arg.Any<CancellationToken>())
            .Returns(Detail(9, ProjectTemplateKinds.PlainText, ProjectContentModes.FreeText, null, "Merhaba!", withTarget: false));

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/9/send/preview", new { campaign_id = "c3" });

        resp.StatusCode.Should().Be((HttpStatusCode)422);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-075");
    }

    [Fact]
    public async Task PreviewSend_FreeText_SnapshotsInline_NotTemplate()
    {
        _factory.FakeProjectsRepo.GetAsync(Arg.Any<int>(), Arg.Is<long>(11), Arg.Any<CancellationToken>())
            .Returns(Detail(11, ProjectTemplateKinds.PlainText, ProjectContentModes.FreeText, null, "Merhaba dünya!", withTarget: true));
        _factory.FakeBulkRepo.CreatePreviewJobFromProjectAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<int?>(), Arg.Any<string?>(),
                Arg.Any<long[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<BulkSendRepository.HsmSnapshotInput?>())
            .Returns(new BulkSendRepository.ProjectSnapshotResult { JobId = 42, Snapshotted = 3 });
        _factory.FakeBulkRepo.GetRecipientPhonesSampleAsync(Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "+905551112233" });

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/11/send/preview", new { campaign_id = "c4" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<BulkSendPreviewResponse>();
        body.Should().NotBeNull();
        (body?.TotalValid ?? -1).Should().Be(3);

        // free_text -> inline carrier: template_id NULL, inline_message_text = the project body, NO hsm.
        await _factory.FakeBulkRepo.Received(1).CreatePreviewJobFromProjectAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Is<long>(11),
            Arg.Is<int?>(t => t == null), Arg.Is<string?>(s => s == "Merhaba dünya!"),
            Arg.Any<long[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Is<BulkSendRepository.HsmSnapshotInput?>(h => h == null));
    }

    [Fact]
    public async Task PreviewSend_GalleryTemplate_SnapshotsTemplateId_NotInline()
    {
        _factory.FakeProjectsRepo.GetAsync(Arg.Any<int>(), Arg.Is<long>(12), Arg.Any<CancellationToken>())
            .Returns(Detail(12, ProjectTemplateKinds.PlainText, ProjectContentModes.GalleryTemplate, outboundTemplateId: 77, null, withTarget: true));
        _factory.FakeBulkRepo.CreatePreviewJobFromProjectAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<int?>(), Arg.Any<string?>(),
                Arg.Any<long[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<BulkSendRepository.HsmSnapshotInput?>())
            .Returns(new BulkSendRepository.ProjectSnapshotResult { JobId = 43, Snapshotted = 2 });
        _factory.FakeBulkRepo.GetRecipientPhonesSampleAsync(Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/12/send/preview", new { campaign_id = "c5" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // gallery_template -> template carrier: template_id = the gallery template id, inline NULL.
        await _factory.FakeBulkRepo.Received(1).CreatePreviewJobFromProjectAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Is<long>(12),
            Arg.Is<int?>(t => t == 77), Arg.Is<string?>(s => s == null),
            Arg.Any<long[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Is<BulkSendRepository.HsmSnapshotInput?>(h => h == null));
    }
}
