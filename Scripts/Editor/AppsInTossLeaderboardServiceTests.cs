#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

public class AppsInTossLeaderboardServiceTests
{
    private FakeSave _save;
    private FakeApi _api;
    private AppsInTossLeaderboardService _service;

    [SetUp]
    public void SetUp()
    {
        _save = new FakeSave();
        _api = new FakeApi();
        _service = CreateService();
    }

    [Test]
    public void SubmitAfterInitializeSendsSavedHighestWithoutOpening()
    {
        _save.Data.stage.highestClearedIndex = 3;
        _service.InitializeAsync().GetAwaiter().GetResult();
        _service.SubmitHighestStage(3);
        Assert.AreEqual(1, _api.SupportCalls);
        Assert.AreEqual(0, _api.OpenCalls);
        CollectionAssert.AreEqual(new[] { "4" }, _api.Scores);
    }

    [Test]
    public void InitializeWithoutProgressSubmitsNothing()
    {
        _service.InitializeAsync().GetAwaiter().GetResult();
        Assert.AreEqual(1, _api.SupportCalls);
        Assert.IsEmpty(_api.Scores);
        Assert.AreEqual(0, _save.SaveCalls);
    }

    [Test]
    public void SubmissionUsesOneBasedSavedHighest()
    {
        _save.Data.stage.highestClearedIndex = 6;
        _service.SubmitHighestStage(2);
        CollectionAssert.AreEqual(new[] { "7" }, _api.Scores);
        Assert.AreEqual(1, _save.SaveCalls);
    }

    [Test]
    public void ReadOnlySaveNeverSubmits()
    {
        _save.ReadOnly = true;
        _save.Data.stage.highestClearedIndex = 6;
        _service.SubmitHighestStage(6);
        Assert.IsEmpty(_api.Scores);
        Assert.AreEqual(0, _save.SaveCalls);
    }

    [Test]
    public void DebugUnlockNeverCallsPlatform()
    {
        _service = CreateService(true);
        _save.Data.stage.highestClearedIndex = 6;
        _service.InitializeAsync().GetAwaiter().GetResult();
        _service.SubmitHighestStage(6);
        _service.ShowLeaderboard();
        Assert.IsFalse(_service.IsAvailable);
        Assert.AreEqual(0, _api.SupportCalls);
        Assert.AreEqual(0, _api.OpenCalls);
        Assert.IsEmpty(_api.Scores);
    }

    [Test]
    public void InvalidSavedProgressNeverSubmits()
    {
        _save.Data.stage.highestClearedIndex = 20;
        _service.SubmitHighestStage(5);
        Assert.IsEmpty(_api.Scores);
        Assert.AreEqual(0, _save.SaveCalls);
    }

    [Test]
    public void SaveFailureDefersSubmissionUntilNextClear()
    {
        _save.Data.stage.highestClearedIndex = 1;
        _save.SaveResult = false;
        _service.SubmitHighestStage(1);
        Assert.IsEmpty(_api.Scores);
        _save.SaveResult = true;
        _service.SubmitHighestStage(1);
        CollectionAssert.AreEqual(new[] { "2" }, _api.Scores);
    }

    [UnityTest]
    public IEnumerator SubmissionWaitsForSaveCompletion() => UniTask.ToCoroutine(async () =>
    {
        var gate = new UniTaskCompletionSource<bool>();
        _save.Data.stage.highestClearedIndex = 2;
        _save.SaveOverride = () => gate.Task;
        _service.SubmitHighestStage(2);
        Assert.IsEmpty(_api.Scores);
        gate.TrySetResult(true);
        await UniTask.Yield();
        CollectionAssert.AreEqual(new[] { "3" }, _api.Scores);
    });

    [TestCase("PROFILE_NOT_FOUND")]
    [TestCase("LEADERBOARD_NOT_FOUND")]
    [TestCase((string)null)]
    public void RejectedSubmissionIsSentAgainOnNextClear(string rejectedStatus)
    {
        _save.Data.stage.highestClearedIndex = 3;
        _api.SubmitStatus = rejectedStatus;
        _service.SubmitHighestStage(3);
        _api.SubmitStatus = "SUCCESS";
        _service.SubmitHighestStage(3);
        CollectionAssert.AreEqual(new[] { "4", "4" }, _api.Scores);
    }

    [Test]
    public void SubmitExceptionIsContainedAndNextClearSubmits()
    {
        _save.Data.stage.highestClearedIndex = 3;
        _api.SubmitOverride = () => UniTask.FromException<string>(new TimeoutException("제출 응답 지연"));
        Assert.DoesNotThrow(() => _service.SubmitHighestStage(3));
        _api.SubmitOverride = null;
        _service.SubmitHighestStage(3);
        CollectionAssert.AreEqual(new[] { "4", "4" }, _api.Scores);
    }

    [UnityTest]
    public IEnumerator OverlappingClearIsSkippedUntilNextSubmit() => UniTask.ToCoroutine(async () =>
    {
        var gate = new UniTaskCompletionSource<string>();
        _api.SubmitOverride = () => _api.Scores.Count == 1 ? gate.Task : UniTask.FromResult("SUCCESS");
        _save.Data.stage.highestClearedIndex = 1;
        _service.SubmitHighestStage(1);
        _save.Data.stage.highestClearedIndex = 7;
        _service.SubmitHighestStage(7);
        gate.TrySetResult("SUCCESS");
        await UniTask.Yield();
        CollectionAssert.AreEqual(new[] { "2" }, _api.Scores);
        _service.SubmitHighestStage(7);
        CollectionAssert.AreEqual(new[] { "2", "8" }, _api.Scores);
    });

    [Test]
    public void ShowOpensWithoutSubmitting()
    {
        _save.Data.stage.highestClearedIndex = 4;
        _service.ShowLeaderboard();
        Assert.AreEqual(1, _api.OpenCalls);
        Assert.IsEmpty(_api.Scores);
        Assert.AreEqual(0, _save.SaveCalls);
    }

    [Test]
    public void OpenFailureIsContained()
    {
        _api.OpenException = new TimeoutException("순위 화면 응답 지연");
        Assert.DoesNotThrow(() => _service.ShowLeaderboard());
        Assert.AreEqual(1, _api.OpenCalls);
    }

    [Test]
    public void UnsupportedVersionDisablesLeaderboard()
    {
        _api.Supported = false;
        _save.Data.stage.highestClearedIndex = 2;
        _service.InitializeAsync().GetAwaiter().GetResult();
        _service.SubmitHighestStage(2);
        _service.ShowLeaderboard();
        Assert.IsFalse(_service.IsAvailable);
        Assert.AreEqual(0, _api.OpenCalls);
        Assert.IsEmpty(_api.Scores);
    }

    [UnityTest]
    public IEnumerator VersionCheckSupportsConcurrentInitializationAndSubmission() => UniTask.ToCoroutine(async () =>
    {
        var gate = new UniTaskCompletionSource<bool>();
        _api.SupportOverride = () => gate.Task;
        _save.Data.stage.highestClearedIndex = 1;
        var initialization = _service.InitializeAsync();
        _service.SubmitHighestStage(1);
        Assert.AreEqual(1, _api.SupportCalls);
        Assert.IsEmpty(_api.Scores);
        gate.TrySetResult(true);
        await initialization;
        CollectionAssert.AreEqual(new[] { "2" }, _api.Scores);
    });

    private AppsInTossLeaderboardService CreateService(bool debugUnlock = false) =>
        new(_save, _api, 20, debugUnlock);

    private sealed class FakeSave : ISaveService
    {
        public bool ReadOnly;
        public bool SaveResult = true;
        public int SaveCalls;
        public Func<UniTask<bool>> SaveOverride;
        public SaveData Data { get; } = new();
        public bool IsReadOnly => ReadOnly;
        public void Save() { }
        public void DeleteAll() { }
        public UniTask<bool> SaveAsync()
        {
            SaveCalls++;
            return SaveOverride != null ? SaveOverride() : UniTask.FromResult(SaveResult);
        }
    }

    private sealed class FakeApi : IAppsInTossLeaderboardApi
    {
        public bool Supported = true;
        public string SubmitStatus = "SUCCESS";
        public int SupportCalls;
        public int OpenCalls;
        public readonly List<string> Scores = new();
        public Func<UniTask<string>> SubmitOverride;
        public Exception OpenException;
        public Func<UniTask<bool>> SupportOverride;

        public UniTask<bool> IsSupportedAsync()
        {
            SupportCalls++;
            return SupportOverride != null ? SupportOverride() : UniTask.FromResult(Supported);
        }
        public UniTask<string> SubmitScoreAsync(string score)
        {
            Scores.Add(score);
            return SubmitOverride != null ? SubmitOverride() : UniTask.FromResult(SubmitStatus);
        }
        public UniTask OpenAsync()
        {
            OpenCalls++;
            return OpenException != null ? UniTask.FromException(OpenException) : UniTask.CompletedTask;
        }
    }
}
#endif
