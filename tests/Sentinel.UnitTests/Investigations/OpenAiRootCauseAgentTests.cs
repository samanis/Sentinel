using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentinel.Application.AI;
using Sentinel.Application.Investigations.Analysis;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Investigations;
using Sentinel.Infrastructure.AI;

namespace Sentinel.UnitTests.Investigations;

public sealed class OpenAiRootCauseAgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MapsStructuredResponseToUntrustedProposal()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var structuredOutput = $$"""
            {
              "relationships": [{
                "sourceEvidenceId": "{{firstId}}",
                "targetEvidenceId": "{{secondId}}",
                "type": "Corroborates",
                "strength": "Exact",
                "explanation": "Both items share a trace identifier."
              }],
              "hypotheses": [{
                "scope": "Event",
                "statement": "A downstream timeout caused the request failure.",
                "confidence": "High",
                "reasoning": "The trace and log report the same timeout.",
                "evidence": [
                  { "evidenceId": "{{firstId}}", "role": "Supporting" },
                  { "evidenceId": "{{secondId}}", "role": "Supporting" }
                ]
              }]
            }
            """;
        var handler = new StubHandler(Response(structuredOutput));
        var agent = Agent(handler);

        var result = await agent.AnalyzeAsync(Input(firstId, secondId), default);

        Assert.Equal("gpt-5.6-sol", result.Model);
        Assert.Equal(RootCauseAgent.PromptVersion, result.PromptVersion);
        Assert.Equal(CorrelationStrength.Exact, Assert.Single(result.Relationships).Strength);
        Assert.Equal(HypothesisConfidence.High, Assert.Single(result.Hypotheses).Confidence);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.False(request.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal("json_schema", request.RootElement
            .GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.True(request.RootElement
            .GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        Assert.Contains(firstId.ToString(), request.RootElement.GetProperty("input").GetString());
    }

    [Fact]
    public async Task MissingApiKeyFailsBeforeSendingRequest()
    {
        var handler = new StubHandler(Response("{}"));
        var agent = Agent(handler, apiKey: "");

        var exception = await Assert.ThrowsAsync<StructuredModelException>(() =>
            agent.AnalyzeAsync(Input(Guid.NewGuid(), Guid.NewGuid()), default));

        Assert.Contains("provider is unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(handler.RequestBody);
    }

    [Fact]
    public async Task NonSuccessResponseIsReportedWithoutLeakingResponseBody()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("secret provider response")
        });
        var agent = Agent(handler);

        var exception = await Assert.ThrowsAsync<StructuredModelException>(() =>
            agent.AnalyzeAsync(Input(Guid.NewGuid(), Guid.NewGuid()), default));

        Assert.Contains("HTTP 401", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret provider response", exception.Message, StringComparison.Ordinal);
    }

    private static RootCauseAgent Agent(StubHandler handler, string apiKey = "test-key") => new(
        new OpenAiStructuredModelClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") },
            Options.Create(new OpenAiModelOptions { ApiKey = apiKey })));

    private static InvestigationAnalysisInput Input(Guid firstId, Guid secondId) => new(
        Guid.NewGuid(),
        "Order failures",
        "incidentlab-order-api",
        Now.AddMinutes(-5),
        [
            new(firstId, EvidenceType.Trace, EvidenceAnalysisScope.Event, Now, "A request timed out.", "trace-1", "span-1", "incidentlab-order-api"),
            new(secondId, EvidenceType.Log, EvidenceAnalysisScope.Event, Now, "A timeout was logged.", "trace-1", "span-2", "incidentlab-order-api")
        ]);

    private static HttpResponseMessage Response(string outputText)
    {
        var body = JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[] { new { type = "output_text", text = outputText } }
                }
            }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
