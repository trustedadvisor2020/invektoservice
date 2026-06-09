using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using Invekto.Outbound.Data;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.DTOs.Outbound;
using NSubstitute;

namespace InvektoServis.Tests.Outbound;

/// <summary>
/// FEAT-PROJELER / cxapi PR-4 — the HSM run validation matrix BEHIND the CxapiSend allowlist
/// (this factory opens it; the closed-gate INV-OB-085 pin lives in <see cref="ProjectSendTests"/>).
/// The live cxapi /api/templates catalog is canned per test (<c>TemplateCatalog</c>), so slug
/// ownership (081), required-param coverage (082), dynamic-BUTTON reject (083), catalog
/// hard-fail (084) and creds (062) all run through the REAL WapCrmTemplateClient parse path.
/// The snapshot itself is faked (no Postgres) — the OK path asserts WHAT the service hands the
/// snapshot (validated HsmSnapshotInput: slug, required keys, column bindings).
/// </summary>
public class ProjectsServiceHsmTests : IClassFixture<OutboundProjectsHsmTestFactory>
{
    private readonly OutboundProjectsHsmTestFactory _factory;
    private readonly HttpClient _client;

    public ProjectsServiceHsmTests(OutboundProjectsHsmTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // ── helpers ────────────────────────────────────────────────────────

    /// <summary>An HSM project with the SPA's param_mapping shape (camelCase entries).</summary>
    private static ProjectDetail HsmDetail(long id, string slug, object[]? mapping)
        => new()
        {
            Project = new ProjectSummary
            {
                Id = id,
                Name = "HSM Proje",
                Status = "draft",
                TargetCount = 1,
                TemplateKind = ProjectTemplateKinds.WapcrmTemplate,
                InstanceId = 555,
                WaTemplateId = slug,
                TemplateLanguage = "tr",
                ParamMapping = mapping != null ? JsonSerializer.SerializeToElement(mapping) : null,
            },
            Targets = new List<ProjectTargetDto>
            {
                new() { DataListId = 10, ListName = "Liste", TotalRecords = 5, SendableCount = 3, SortOrder = 0 },
            },
        };

    private static object ColumnParam(string paramKey, string column)
        => new { kind = "text", location = "BODY", paramKey, mediaType = (string?)null, source = "column", column };

    /// <summary>Cans the cxapi /api/templates envelope (status=true) with the given template DTOs.</summary>
    private void CanCatalog(params object[] templates)
    {
        _factory.TemplateCatalog.Clear();
        var body = JsonSerializer.Serialize(new
        {
            status = true,
            message = "ok",
            statusCode = "200",
            requestID = "req-test",
            data = templates,
        });
        _factory.TemplateCatalog.WhenUrl("api/templates", HttpStatusCode.OK, body);
    }

    private static object CatalogTemplate(string slug, params object[] requiredInputs)
        => new { templateId = slug, name = slug, language = "tr", category = "MARKETING", requiredInputs };

    private void StubCreds()
        => _factory.FakeRepo.GetWapCrmSettingsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new WapCrmSettings { SecretKey = "sk-test", UserId = 7, InstanceId = 555 });

    private void StubProject(long id, ProjectDetail detail)
        => _factory.FakeProjectsRepo.GetAsync(Arg.Any<int>(), Arg.Is<long>(id), Arg.Any<CancellationToken>())
            .Returns(detail);

    // ── OK path: validated config reaches the snapshot ─────────────────

    [Fact]
    public async Task Preview_Hsm_Valid_SnapshotsHsmConfig_WithRequiredKeys_AndSkippedPassthrough()
    {
        StubCreds();
        CanCatalog(CatalogTemplate("kampanya_tr",
            new { kind = "text", location = "BODY", paramKey = "ad" },
            new { kind = "text", location = "BODY", paramKey = "kupon" }));
        StubProject(21, HsmDetail(21, "kampanya_tr", new[]
        {
            ColumnParam("ad", "name"),
            new { kind = "text", location = "BODY", paramKey = "kupon", mediaType = (string?)null, source = "literal", value = "INDIRIM10" },
        }));
        _factory.FakeBulkRepo.ClearReceivedCalls();
        _factory.FakeBulkRepo.CreatePreviewJobFromProjectAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<int?>(), Arg.Any<string?>(),
                Arg.Any<long[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<BulkSendRepository.HsmSnapshotInput?>())
            .Returns(new BulkSendRepository.ProjectSnapshotResult
            {
                JobId = 99, Snapshotted = 3, SkippedMissingParams = 2,
                SkippedParamsJson = "{\"count\":2,\"by_param\":{\"ad\":2},\"sample\":[]}",
            });
        _factory.FakeBulkRepo.GetRecipientPhonesSampleAsync(Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "+905551112233" });

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/21/send/preview", new { campaign_id = "h1" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<BulkSendPreviewResponse>();
        (body?.TotalValid ?? -1).Should().Be(3);
        // PR-4: the preview surfaces the skip audit (count + per-param breakdown).
        (body?.SkippedParams?.Count ?? -1).Should().Be(2);
        (body?.SkippedParams?.ByParam?["ad"] ?? -1).Should().Be(2);
        (body?.TotalInput ?? -1).Should().Be(5); // snapshotted + skipped

        // The snapshot received the VALIDATED config: slug + the live template's required keys +
        // the column binding (literal params don't ride the column eligibility set).
        await _factory.FakeBulkRepo.Received(1).CreatePreviewJobFromProjectAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Is<long>(21),
            Arg.Is<int?>(t => t == null), Arg.Is<string?>(s => s == null),
            Arg.Any<long[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>(),
            Arg.Is<BulkSendRepository.HsmSnapshotInput?>(h =>
                h != null
                && h.WaTemplateId == "kampanya_tr"
                && h.InstanceId == 555
                && h.RequiredParamKeys.Contains("ad")
                && h.RequiredParamKeys.Contains("kupon")
                && h.TextParamColumns.Any(b => b.Key == "ad" && b.Value == "name")
                && h.TextParamLiterals.Any(b => b.Key == "kupon" && b.Value == "INDIRIM10")));
    }

    // ── 081: slug absent from the live catalog ─────────────────────────

    [Fact]
    public async Task Preview_Hsm_SlugNotInCatalog_Rejected_Inv081()
    {
        StubCreds();
        CanCatalog(CatalogTemplate("baska_sablon_tr"));
        StubProject(22, HsmDetail(22, "silinmis_sablon_tr", new[] { ColumnParam("ad", "name") }));

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/22/send/preview", new { campaign_id = "h2" });

        resp.StatusCode.Should().Be((HttpStatusCode)422);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-081");
    }

    // ── 082: a live required param is not covered by the mapping ───────

    [Fact]
    public async Task Preview_Hsm_RequiredParamUnmapped_Rejected_Inv082()
    {
        StubCreds();
        CanCatalog(CatalogTemplate("kampanya_tr",
            new { kind = "text", location = "BODY", paramKey = "ad" },
            new { kind = "text", location = "BODY", paramKey = "soyad" })); // soyad not mapped
        StubProject(23, HsmDetail(23, "kampanya_tr", new[] { ColumnParam("ad", "name") }));

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/23/send/preview", new { campaign_id = "h3" });

        resp.StatusCode.Should().Be((HttpStatusCode)422);
        var text = await resp.Content.ReadAsStringAsync();
        text.Should().Contain("INV-OB-082");
        text.Should().Contain("soyad"); // the operator sees WHICH param is missing
    }

    // ── 083: dynamic BUTTON param (G13.3 deferred) ─────────────────────

    [Fact]
    public async Task Preview_Hsm_DynamicButtonTemplate_Rejected_Inv083()
    {
        StubCreds();
        CanCatalog(CatalogTemplate("buton_sablon_tr",
            new { kind = "text", location = "BUTTON", paramKey = "btn1_url" }));
        StubProject(24, HsmDetail(24, "buton_sablon_tr", new[] { ColumnParam("btn1_url", "field1") }));

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/24/send/preview", new { campaign_id = "h4" });

        resp.StatusCode.Should().Be((HttpStatusCode)422);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-083");
    }

    // ── 084: catalog unreachable -> HARD FAIL (no stale/silent continue) ──

    [Fact]
    public async Task Preview_Hsm_CatalogUnreachable_HardFail_Inv084()
    {
        StubCreds();
        _factory.TemplateCatalog.Clear(); // no handler -> 502 from the stub -> TransportError
        StubProject(25, HsmDetail(25, "kampanya_tr", new[] { ColumnParam("ad", "name") }));
        _factory.FakeBulkRepo.ClearReceivedCalls();

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/25/send/preview", new { campaign_id = "h5" });

        resp.StatusCode.Should().Be((HttpStatusCode)503);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-084");
        // Hard fail means NO snapshot was attempted with unvalidated config.
        await _factory.FakeBulkRepo.DidNotReceive().CreatePreviewJobFromProjectAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<long[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<BulkSendRepository.HsmSnapshotInput?>());
    }

    // ── 062: WapCRM creds missing ──────────────────────────────────────

    [Fact]
    public async Task Preview_Hsm_MissingCreds_Rejected_Inv062()
    {
        _factory.FakeRepo.GetWapCrmSettingsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((WapCrmSettings?)null);
        CanCatalog(CatalogTemplate("kampanya_tr"));
        StubProject(26, HsmDetail(26, "kampanya_tr", new[] { ColumnParam("ad", "name") }));

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/26/send/preview", new { campaign_id = "h6" });

        resp.StatusCode.Should().Be((HttpStatusCode)422);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-062");
    }

    // ── 073: unknown list column in the mapping ────────────────────────

    [Fact]
    public async Task Preview_Hsm_UnknownListColumn_Rejected_Inv073()
    {
        StubCreds();
        CanCatalog(CatalogTemplate("kampanya_tr", new { kind = "text", location = "BODY", paramKey = "ad" }));
        StubProject(27, HsmDetail(27, "kampanya_tr", new[] { ColumnParam("ad", "olmayan_kolon") }));

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/27/send/preview", new { campaign_id = "h7" });

        ((int)resp.StatusCode).Should().Be(400);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INV-OB-073");
    }

    // ── 073: malformed kind/source values are typed rejects (never guessed) ──

    [Fact]
    public async Task Preview_Hsm_UnknownKindOrSource_Rejected_Inv073()
    {
        StubCreds();
        CanCatalog(CatalogTemplate("kampanya_tr", new { kind = "text", location = "BODY", paramKey = "ad" }));

        // Unknown kind — must reject, never coerced into a text binding.
        StubProject(31, HsmDetail(31, "kampanya_tr", new object[]
        {
            new { kind = "interactive", location = "BODY", paramKey = "ad", mediaType = (string?)null, source = "column", column = "name" },
        }));
        var r1 = await _client.PostAsJsonAsync("/api/v1/projects/31/send/preview", new { campaign_id = "h9" });
        ((int)r1.StatusCode).Should().Be(400);
        (await r1.Content.ReadAsStringAsync()).Should().Contain("INV-OB-073");

        // Unknown source — must reject, never coerced into a literal binding.
        StubProject(32, HsmDetail(32, "kampanya_tr", new object[]
        {
            new { kind = "text", location = "BODY", paramKey = "ad", mediaType = (string?)null, source = "garbled", value = "X" },
        }));
        var r2 = await _client.PostAsJsonAsync("/api/v1/projects/32/send/preview", new { campaign_id = "h10" });
        ((int)r2.StatusCode).Should().Be(400);
        (await r2.Content.ReadAsStringAsync()).Should().Contain("INV-OB-073");

        // Unknown source on a MEDIA entry — the source domain is enforced for every entry,
        // media included (iter1 finding: media must not bypass the source check).
        StubProject(33, HsmDetail(33, "kampanya_tr", new object[]
        {
            new { kind = "media", location = "HEADER", paramKey = (string?)null, mediaType = "image", source = "garbled", value = "https://cdn.example.com/a.jpg" },
        }));
        var r3 = await _client.PostAsJsonAsync("/api/v1/projects/33/send/preview", new { campaign_id = "h11" });
        ((int)r3.StatusCode).Should().Be(400);
        (await r3.Content.ReadAsStringAsync()).Should().Contain("INV-OB-073");
    }

    // ── confirm: content family changed after preview -> re-preview ────

    [Fact]
    public async Task Confirm_HsmJob_ProjectSwitchedToPlainText_Rejected_RePreview()
    {
        // The job snapshot is HSM but the live project is now plain_text — a fresh dispatch of the
        // stale preview must reject (the operator no longer intends template content).
        var plainDetail = new ProjectDetail
        {
            Project = new ProjectSummary
            {
                Id = 28, Name = "Degisen Proje", Status = "draft", TargetCount = 1,
                TemplateKind = ProjectTemplateKinds.PlainText, ContentMode = ProjectContentModes.FreeText,
                PlainTextBody = "Merhaba", InstanceId = 555,
            },
            Targets = new List<ProjectTargetDto> { new() { DataListId = 10, ListName = "L", TotalRecords = 5, SendableCount = 3, SortOrder = 0 } },
        };
        StubProject(28, plainDetail);
        _factory.FakeBulkRepo.GetJobAsync(Arg.Any<int>(), Arg.Is<string>("h8"), Arg.Any<CancellationToken>())
            .Returns(new BulkSendRepository.JobRecord
            {
                Id = 77, TenantId = 1, CampaignId = "h8", Status = "preview_ready", ProjectId = 28,
                WaTemplateId = "kampanya_tr", TemplateKind = "wapcrm_template", InstanceId = 555,
                ParamMappingJson = "[]",
            });

        var resp = await _client.PostAsJsonAsync("/api/v1/projects/28/send/confirm", new { campaign_id = "h8" });

        ((int)resp.StatusCode).Should().Be(400);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("önizleme");
    }
}
