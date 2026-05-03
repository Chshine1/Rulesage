using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FSharp.Collections;
using Moq;
using Rulesage.Common.Types.Domain;
using Rulesage.Retrieval.Options;
using Rulesage.Retrieval.Utils;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Retrieval.Tests;

public class OperationRetrievalServiceTests
{
    private static Mock<IEmbeddingService> CreateEmbeddingServiceMock(float[]? vectorToReturn = null)
    {
        var mock = new Mock<IEmbeddingService>();
        mock.Setup(e => e.GetEmbedding(It.IsAny<string>()))
            .Returns(vectorToReturn ?? [0.1f, 0.2f, 0.3f]);
        return mock;
    }

    private static Mock<IOperationRepository> CreateRepositoryMock(
        IEnumerable<(OperationSignature, float)> candidatesToReturn)
    {
        var mock = new Mock<IOperationRepository>();
        mock.Setup(r => r.FindOrderByCosineDistanceAsync(
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidatesToReturn);
        return mock;
    }

    private static Mock<IOperationIdfService> CreateIdfServiceMock(
        Dictionary<string, float> descriptionToIdfMap)
    {
        var mock = new Mock<IOperationIdfService>();
        foreach (var kvp in descriptionToIdfMap)
        {
            mock.Setup(i => i.ComputeAverageIdfAsync(kvp.Key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(kvp.Value);
        }

        mock.Setup(i => i.ComputeAverageIdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.0f);
        return mock;
    }

    private static RetrievalOptions _defaultOptions => new()
    {
        CoarseRecallSize = 20,
        FinalTopK = 5,
        LevelAlignmentSigma = 0.5f,
        IdfPenaltyBeta = 0.2f
    };

    private static OptionsWrapper<RetrievalOptions> WrapOptions(RetrievalOptions options) => new(options);

    private static OperationSignature[] ComputeExpectedTopK(
        (OperationSignature, float)[] coarseCandidates,
        Dictionary<string, float> idfMap,
        float tau,
        RetrievalOptions options)
    {
        var scored = coarseCandidates.Select(c =>
        {
            var cosineSim = 1.0f - c.Item2;
            var levelFactor = OperationRetrievalUtils.ComputeLevelFactor(
                c.Item1.level, tau, options.LevelAlignmentSigma);
            var idf = idfMap.GetValueOrDefault(c.Item1.description, 0.0f);
            var decayFactor = OperationRetrievalUtils.ComputeDecayFactor(
                idf, options.IdfPenaltyBeta);
            var finalScore = cosineSim * levelFactor * decayFactor;
            return new
            {
                Operation = c.Item1,
                FinalScore = finalScore
            };
        });

        return scored
            .OrderByDescending(x => x.FinalScore)
            .Take(options.FinalTopK)
            .Select(x => x.Operation)
            .ToArray();
    }

    private static OperationSignature CreateOp(int id, string desc, float level) =>
        new(new Identifier(id, ""), desc, level, MapModule.Empty<string, ParamType>(),
            MapModule.Empty<string, ParamType>());

    [Fact]
    public async Task RetrieveAsync_MoreCandidatesThanTopK_ReturnsTopKOrderedByScore()
    {
        // Arrange
        var embeddingVector = new[] { 1.0f, 2.0f };
        var ops = new[]
        {
            (CreateOp(1, "desc1", 0.8f), 0.2f),
            (CreateOp(2, "desc2", 1.2f), 0.25f),
            (CreateOp(3, "desc3", 1.0f), 0.1f),
            (CreateOp(4, "desc4", 0.9f), 0.15f),
            (CreateOp(5, "desc5", 1.1f), 0.3f),
            (CreateOp(6, "desc6", 0.7f), 0.05f),
            (CreateOp(7, "desc7", 1.3f), 0.4f),
            (CreateOp(8, "desc8", 1.5f), 0.22f),
            (CreateOp(9, "desc9", 1.0f), 0.18f),
            (CreateOp(10, "desc10", 0.6f), 0.12f)
        };
        var idfMap = new Dictionary<string, float>
        {
            ["desc1"] = 0.5f, ["desc2"] = 0.8f, ["desc3"] = 0.3f, ["desc4"] = 0.9f,
            ["desc5"] = 0.2f, ["desc6"] = 0.7f, ["desc7"] = 0.4f, ["desc8"] = 0.1f,
            ["desc9"] = 0.6f, ["desc10"] = 0.95f
        };

        var mockEmbed = CreateEmbeddingServiceMock(embeddingVector);
        var mockRepo = CreateRepositoryMock(ops);
        var mockIdf = CreateIdfServiceMock(idfMap);
        var options = WrapOptions(_defaultOptions);
        var logger = NullLogger<OperationRetrievalService>.Instance;
        var service = new OperationRetrievalService(
            mockEmbed.Object, mockRepo.Object, mockIdf.Object, options, logger);

        // Act
        var result = await service.RetrieveAsync("test task");

        // Assert
        Assert.Equal(5, result.Length);
        var expected = ComputeExpectedTopK(ops, idfMap, 1.0f, _defaultOptions);
        Assert.True(expected.SequenceEqual(result, ReferenceEqualityComparer.Instance));

        mockEmbed.Verify(e => e.GetEmbedding("test task"), Times.Once);
        mockRepo.Verify(r => r.FindOrderByCosineDistanceAsync(
            embeddingVector, 0, _defaultOptions.CoarseRecallSize, CancellationToken.None), Times.Once);
        mockIdf.Verify(i => i.ComputeAverageIdfAsync(
            It.IsAny<string>(), CancellationToken.None), Times.Exactly(ops.Length));
    }

    [Fact]
    public async Task RetrieveAsync_FewerCandidatesThanTopK_ReturnsAllInOrder()
    {
        var embedding = new[] { 0.5f };
        var ops = new[]
        {
            (CreateOp(1, "d1", 1.0f), 0.1f),
            (CreateOp(2, "d2", 0.8f), 0.3f)
        };
        var idfMap = new Dictionary<string, float> { ["d1"] = 0.2f, ["d2"] = 0.5f };
        var options = new RetrievalOptions
        {
            CoarseRecallSize = 10,
            FinalTopK = 5,
            LevelAlignmentSigma = 0.3f,
            IdfPenaltyBeta = 0.1f
        };

        var service = new OperationRetrievalService(
            CreateEmbeddingServiceMock(embedding).Object,
            CreateRepositoryMock(ops).Object,
            CreateIdfServiceMock(idfMap).Object,
            WrapOptions(options),
            NullLogger<OperationRetrievalService>.Instance);

        var result = await service.RetrieveAsync("query");

        Assert.Equal(2, result.Length);
        var expected = ComputeExpectedTopK(ops, idfMap, 1.0f, options);
        Assert.True(expected.SequenceEqual(result, ReferenceEqualityComparer.Instance));
    }

    [Fact]
    public async Task RetrieveAsync_EmptyCoarseRecall_ReturnsEmptyArray()
    {
        var mockRepo = new Mock<IOperationRepository>();
        mockRepo.Setup(r => r.FindOrderByCosineDistanceAsync(
                It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new OperationRetrievalService(
            CreateEmbeddingServiceMock().Object,
            mockRepo.Object,
            new Mock<IOperationIdfService>().Object,
            WrapOptions(_defaultOptions),
            NullLogger<OperationRetrievalService>.Instance);

        var result = await service.RetrieveAsync("task");

        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveAsync_CandidatesEqualToTopK_ReturnsAll()
    {
        var ops = Enumerable.Range(1, 5)
            .Select(i => (CreateOp(i, $"desc{i}", 1.0f), 0.1f * i))
            .ToArray();
        var idfMap = ops.ToDictionary(
            t => t.Item1.description,
            t => 0.3f + 0.05f * t.Item1.id.id);

        var service = new OperationRetrievalService(
            CreateEmbeddingServiceMock().Object,
            CreateRepositoryMock(ops).Object,
            CreateIdfServiceMock(idfMap).Object,
            WrapOptions(_defaultOptions),
            NullLogger<OperationRetrievalService>.Instance);

        var result = await service.RetrieveAsync("x");

        Assert.Equal(5, result.Length);
        var expected = ComputeExpectedTopK(ops, idfMap, 1.0f, _defaultOptions);
        Assert.True(expected.SequenceEqual(result, ReferenceEqualityComparer.Instance));
    }

    [Fact]
    public async Task RetrieveAsync_LevelFactorChangesOrder()
    {
        var op1 = CreateOp(1, "desc1", 0.9f);
        var op2 = CreateOp(2, "desc2", 2.0f);
        var ops = new[]
        {
            (op1, 0.1f), // cosine sim = 0.9
            (op2, 0.1f) // cosine sim = 0.9
        };
        var idfMap = new Dictionary<string, float> { ["desc1"] = 0.0f, ["desc2"] = 0.0f };

        var service = new OperationRetrievalService(
            CreateEmbeddingServiceMock().Object,
            CreateRepositoryMock(ops).Object,
            CreateIdfServiceMock(idfMap).Object,
            WrapOptions(new RetrievalOptions
            {
                CoarseRecallSize = 10,
                FinalTopK = 2,
                LevelAlignmentSigma = 0.5f,
                IdfPenaltyBeta = 0.0f
            }),
            NullLogger<OperationRetrievalService>.Instance);

        var result = await service.RetrieveAsync("t");

        Assert.Equal(2, result.Length);
        Assert.Same(op1, result[0]);
        Assert.Same(op2, result[1]);
    }

    [Fact]
    public async Task RetrieveAsync_IdfDecayChangesOrder()
    {
        var op1 = CreateOp(1, "desc1", 1.0f);
        var op2 = CreateOp(2, "desc2", 1.0f);
        var ops = new[]
        {
            (op1, 0.2f), // cosine sim = 0.8
            (op2, 0.2f) // cosine sim = 0.8
        };
        var idfMap = new Dictionary<string, float> { ["desc1"] = 0.1f, ["desc2"] = 0.9f };

        var service = new OperationRetrievalService(
            CreateEmbeddingServiceMock().Object,
            CreateRepositoryMock(ops).Object,
            CreateIdfServiceMock(idfMap).Object,
            WrapOptions(new RetrievalOptions
            {
                CoarseRecallSize = 5,
                FinalTopK = 2,
                LevelAlignmentSigma = 10f, // 所有level因子接近1
                IdfPenaltyBeta = 1.0f // 强衰减
            }),
            NullLogger<OperationRetrievalService>.Instance);

        var result = await service.RetrieveAsync("t");

        Assert.Equal(2, result.Length);
        Assert.Same(op1, result[0]);
        Assert.Same(op2, result[1]);
    }

    [Fact]
    public async Task RetrieveAsync_CustomTargetLevel_AffectsLevelFactor()
    {
        var opClose = CreateOp(1, "desc1", 0.5f);
        var opFar = CreateOp(2, "desc2", 1.5f);
        var ops = new[]
        {
            (opClose, 0.1f),
            (opFar, 0.1f)
        };
        var idfMap = new Dictionary<string, float> { ["desc1"] = 0f, ["desc2"] = 0f };

        var service = new OperationRetrievalService(
            CreateEmbeddingServiceMock().Object,
            CreateRepositoryMock(ops).Object,
            CreateIdfServiceMock(idfMap).Object,
            WrapOptions(new RetrievalOptions
            {
                CoarseRecallSize = 10,
                FinalTopK = 2,
                LevelAlignmentSigma = 0.5f,
                IdfPenaltyBeta = 0f
            }),
            NullLogger<OperationRetrievalService>.Instance);

        var result = await service.RetrieveAsync("task", targetLevel: 0.5f);

        Assert.Equal(2, result.Length);
        Assert.Same(opClose, result[0]);
        Assert.Same(opFar, result[1]);
    }

    [Fact]
    public async Task RetrieveAsync_PassesCancellationTokenToAllAsyncMethods()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var embeddingVector = new[] { 0f };

        var mockEmbed = new Mock<IEmbeddingService>();
        mockEmbed.Setup(e => e.GetEmbedding(It.IsAny<string>())).Returns(embeddingVector);

        var mockRepo = new Mock<IOperationRepository>();
        var capturedRepoToken = CancellationToken.None;
        mockRepo.Setup(r => r.FindOrderByCosineDistanceAsync(
                embeddingVector,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<float[], int, CancellationToken>((_, _, ct) => capturedRepoToken = ct)
            .ReturnsAsync([
                (CreateOp(1, "d1", 1.0f), 0.2f)
            ]);

        var mockIdf = new Mock<IOperationIdfService>();
        var capturedIdfToken = CancellationToken.None;
        mockIdf.Setup(i => i.ComputeAverageIdfAsync("d1", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, ct) => capturedIdfToken = ct)
            .ReturnsAsync(0.5f);

        var service = new OperationRetrievalService(
            mockEmbed.Object,
            mockRepo.Object,
            mockIdf.Object,
            WrapOptions(_defaultOptions),
            NullLogger<OperationRetrievalService>.Instance);

        await service.RetrieveAsync("nl", cancellationToken: token);

        Assert.Equal(token, capturedRepoToken);
        Assert.Equal(token, capturedIdfToken);
    }

    [Fact]
    public async Task RetrieveAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var token = cts.Token;

        var embeddingVector = new[] { 0.1f };
        var mockEmbed = new Mock<IEmbeddingService>();
        mockEmbed.Setup(e => e.GetEmbedding(It.IsAny<string>())).Returns(embeddingVector);

        var mockRepo = new Mock<IOperationRepository>();
        mockRepo.Setup(r => r.FindOrderByCosineDistanceAsync(
                embeddingVector, It.IsAny<int>(), It.IsAny<int>(), token))
            .ThrowsAsync(new OperationCanceledException(token));

        var service = new OperationRetrievalService(
            mockEmbed.Object,
            mockRepo.Object,
            new Mock<IOperationIdfService>().Object,
            WrapOptions(_defaultOptions),
            NullLogger<OperationRetrievalService>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RetrieveAsync("query", cancellationToken: token));
    }

    [Fact]
    public async Task RetrieveAsync_WhenLogLevelDebug_LogsCandidateCount()
    {
        var ops = new[]
        {
            (CreateOp(1, "desc", 1.0f), 0.1f)
        };
        var mockLogger = new Mock<ILogger<OperationRetrievalService>>();
        mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        var service = new OperationRetrievalService(
            CreateEmbeddingServiceMock().Object,
            CreateRepositoryMock(ops).Object,
            CreateIdfServiceMock(new Dictionary<string, float> { ["desc"] = 0.2f }).Object,
            WrapOptions(_defaultOptions),
            mockLogger.Object);

        await service.RetrieveAsync("nl");

#pragma warning disable CA1873
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (v.ToString() ?? "").Contains("Coarse recall returned 1 candidates")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
#pragma warning restore CA1873
    }
}

internal class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
    public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}