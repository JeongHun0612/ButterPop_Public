#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SaveProtectionTests
{
    private string _filePath;

    [SetUp]
    public void SetUp()
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/SaveProtectionTests"));
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".dat");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
        if (File.Exists(_filePath + ".tmp")) File.Delete(_filePath + ".tmp");
    }

    [UnityTest]
    public IEnumerator MissingSaveAllowsFirstWrite() => UniTask.ToCoroutine(async () =>
    {
        var storage = new CountingStorage();
        var service = await SaveService.CreateAsync(storage);

        Assert.IsFalse(service.IsReadOnly);
        Assert.AreEqual(-1, service.Data.stage.highestClearedIndex);
        service.Data.stage.highestClearedIndex = 4;
        Assert.IsTrue(await service.SaveAsync());
        CollectionAssert.AreEqual(new[] { 4 }, storage.SavedStages);
    });

    [UnityTest]
    public IEnumerator LoadedSavePreservesProgressAndPurchases() => UniTask.ToCoroutine(async () =>
    {
        var data = new SaveData();
        data.stage.highestClearedIndex = 42;
        data.purchase.allRemoveAds = true;
        var service = await SaveService.CreateAsync(new CountingStorage { LoadedData = data });

        Assert.IsFalse(service.IsReadOnly);
        Assert.AreEqual(42, service.Data.stage.highestClearedIndex);
        Assert.IsTrue(service.Data.purchase.allRemoveAds);
    });

    [UnityTest]
    public IEnumerator LoadFailureBlocksAllWritesAndDeletes() => UniTask.ToCoroutine(async () =>
    {
        var storage = new CountingStorage { LoadError = new TimeoutException("조회 시간 초과") };
        ExpectLoadFailure();
        var service = await SaveService.CreateAsync(storage);

        Assert.IsTrue(service.IsReadOnly);
        Assert.IsNotNull(service.Data.settings);
        service.Data.stage.highestClearedIndex = 7;
        Assert.IsFalse(await service.SaveAsync());
        service.Save();
        service.DeleteAll();
        Assert.IsFalse(await service.SaveAsync());

        Assert.AreEqual(0, storage.SaveCalls);
        Assert.AreEqual(0, storage.DeleteCalls);
        Assert.AreEqual(7, service.Data.stage.highestClearedIndex);
        Assert.IsTrue(service.IsReadOnly);
    });

    [UnityTest]
    public IEnumerator NextSessionCanLoadOriginalProgress() => UniTask.ToCoroutine(async () =>
    {
        var original = new SaveData();
        original.stage.highestClearedIndex = 23;
        var storage = new CountingStorage { LoadedData = original, LoadError = new IOException("읽기 실패") };
        ExpectLoadFailure();
        var failedSession = await SaveService.CreateAsync(storage);
        failedSession.Data.stage.highestClearedIndex = 1;
        Assert.IsFalse(await failedSession.SaveAsync());

        storage.LoadError = null;
        var nextSession = await SaveService.CreateAsync(storage);
        Assert.IsFalse(nextSession.IsReadOnly);
        Assert.AreEqual(23, nextSession.Data.stage.highestClearedIndex);
        Assert.AreEqual(0, storage.SaveCalls);
    });

    [UnityTest]
    public IEnumerator WritesRemainSerialized() => UniTask.ToCoroutine(async () =>
    {
        var storage = new CountingStorage { FirstSaveGate = new UniTaskCompletionSource<bool>() };
        var service = await SaveService.CreateAsync(storage);
        service.Data.stage.highestClearedIndex = 1;
        var first = service.SaveAsync();
        service.Data.stage.highestClearedIndex = 2;
        var second = service.SaveAsync();
        Assert.AreEqual(1, storage.SaveCalls);

        storage.FirstSaveGate.TrySetResult(true);
        Assert.IsTrue(await first);
        Assert.IsTrue(await second);
        CollectionAssert.AreEqual(new[] { 1, 2 }, storage.SavedStages);
    });

    [UnityTest]
    public IEnumerator FailedWriteDoesNotPoisonLaterWrites() => UniTask.ToCoroutine(async () =>
    {
        var storage = new CountingStorage { SaveResult = false };
        var service = await SaveService.CreateAsync(storage);
        Assert.IsFalse(await service.SaveAsync());
        storage.SaveResult = true;
        Assert.IsTrue(await service.SaveAsync());
        Assert.IsFalse(service.IsReadOnly);
        Assert.AreEqual(2, storage.SaveCalls);
    });

    [UnityTest]
    public IEnumerator MissingFileIsNotALoadFailure() => UniTask.ToCoroutine(async () =>
    {
        var service = await SaveService.CreateAsync(new FileSaveStorage(_filePath));
        Assert.IsFalse(service.IsReadOnly);
        Assert.IsTrue(await service.SaveAsync());
        Assert.IsTrue(File.Exists(_filePath));
    });

    [UnityTest]
    public IEnumerator ValidFileLoadsProgressAndPurchases() => UniTask.ToCoroutine(async () =>
    {
        var data = new SaveData();
        data.stage.highestClearedIndex = 17;
        data.purchase.bannerRemoveAds = true;
        File.WriteAllText(_filePath, JsonUtility.ToJson(data));
        var service = await SaveService.CreateAsync(new FileSaveStorage(_filePath));
        Assert.IsFalse(service.IsReadOnly);
        Assert.AreEqual(17, service.Data.stage.highestClearedIndex);
        Assert.IsTrue(service.Data.purchase.bannerRemoveAds);
    });

    [UnityTest]
    public IEnumerator UnreadableFileIsPreserved() => UniTask.ToCoroutine(async () =>
    {
        string json = JsonUtility.ToJson(new SaveData());
        File.WriteAllText(_filePath, json);
        using (var lockedFile = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            ExpectLoadFailure();
            var service = await SaveService.CreateAsync(new FileSaveStorage(_filePath));
            Assert.IsTrue(service.IsReadOnly);
            Assert.IsFalse(await service.SaveAsync());
            service.DeleteAll();
        }
        Assert.AreEqual(json, File.ReadAllText(_filePath));
    });

    [UnityTest]
    public IEnumerator EmptyFileIsPreserved() => UniTask.ToCoroutine(async () =>
        await VerifyCorruptFileAsync(""));

    [UnityTest]
    public IEnumerator MalformedFileIsPreserved() => UniTask.ToCoroutine(async () =>
        await VerifyCorruptFileAsync("{broken"));

    [UnityTest]
    public IEnumerator NullJsonFileIsPreserved() => UniTask.ToCoroutine(async () =>
        await VerifyCorruptFileAsync("null"));

    // 실제 SDK 브릿지는 키 부재를 C# null이 아니라 문자열 "null"로 전달한다.
    [UnityTest]
    public IEnumerator BridgeMissingKeyAllowsFirstSave() => UniTask.ToCoroutine(async () =>
        await VerifyMissingBridgeValueAsync("null"));

    [UnityTest]
    public IEnumerator BridgeEmptyValueIsMissingKey() => UniTask.ToCoroutine(async () =>
        await VerifyMissingBridgeValueAsync(""));

    [UnityTest]
    public IEnumerator BridgeNullReferenceIsMissingKey() => UniTask.ToCoroutine(async () =>
        await VerifyMissingBridgeValueAsync(null));

    [UnityTest]
    public IEnumerator BridgeTransientFailuresRecoverOriginalProgress() => UniTask.ToCoroutine(async () =>
    {
        var original = new SaveData();
        original.stage.highestClearedIndex = 44;
        original.purchase.allRemoveAds = true;
        var storage = new FakeBridgeStorage();
        storage.Responses.Enqueue(new TimeoutException("시간 초과"));
        storage.Responses.Enqueue(new InvalidOperationException("브릿지 오류"));
        storage.Responses.Enqueue(JsonUtility.ToJson(original));
        var service = await SaveService.CreateAsync(storage);

        Assert.IsFalse(service.IsReadOnly);
        Assert.AreEqual(3, storage.GetCalls);
        Assert.IsTrue(storage.Timeouts.TrueForAll(timeout => timeout > 0), "타임아웃 0은 무한 대기다");
        Assert.AreEqual(44, service.Data.stage.highestClearedIndex);
        Assert.IsTrue(service.Data.purchase.allRemoveAds);
        Assert.IsTrue(await service.SaveAsync());
    });

    [UnityTest]
    public IEnumerator BridgeExhaustedRetriesBlockWrites() => UniTask.ToCoroutine(async () =>
    {
        var storage = new FakeBridgeStorage();
        for (int i = 0; i < 3; i++) storage.Responses.Enqueue(new TimeoutException("시간 초과"));
        ExpectLoadFailure();
        var service = await SaveService.CreateAsync(storage);

        Assert.AreEqual(3, storage.GetCalls);
        Assert.IsTrue(service.IsReadOnly);
        Assert.IsFalse(await service.SaveAsync());
        service.Save();
        service.DeleteAll();
        Assert.AreEqual(0, storage.SaveCalls);
        Assert.AreEqual(0, storage.DeleteCalls);
    });

    [UnityTest]
    public IEnumerator BridgeMalformedValueBlocksWritesWithoutRetry() => UniTask.ToCoroutine(async () =>
    {
        var storage = new FakeBridgeStorage();
        storage.Responses.Enqueue("{broken");
        ExpectLoadFailure();
        var service = await SaveService.CreateAsync(storage);

        Assert.AreEqual(1, storage.GetCalls);
        Assert.IsTrue(service.IsReadOnly);
        Assert.IsFalse(await service.SaveAsync());
        service.DeleteAll();
        Assert.AreEqual(0, storage.SaveCalls);
        Assert.AreEqual(0, storage.DeleteCalls);
    });

    private async UniTask VerifyMissingBridgeValueAsync(string value)
    {
        var storage = new FakeBridgeStorage();
        storage.Responses.Enqueue(value);
        var service = await SaveService.CreateAsync(storage);

        Assert.IsFalse(service.IsReadOnly);
        Assert.AreEqual(1, storage.GetCalls);
        Assert.AreEqual(-1, service.Data.stage.highestClearedIndex);
        Assert.IsTrue(await service.SaveAsync());
        Assert.AreEqual(1, storage.SaveCalls);
    }

    private async UniTask VerifyCorruptFileAsync(string json)
    {
        File.WriteAllText(_filePath, json);
        ExpectLoadFailure();
        var service = await SaveService.CreateAsync(new FileSaveStorage(_filePath));
        Assert.IsTrue(service.IsReadOnly);
        Assert.IsFalse(await service.SaveAsync());
        service.Save();
        service.DeleteAll();
        Assert.IsTrue(File.Exists(_filePath));
        Assert.AreEqual(json, File.ReadAllText(_filePath));
    }

    private static void ExpectLoadFailure() =>
        LogAssert.Expect(LogType.Error, new Regex(@"\[SaveService\] 로드 실패"));

    private sealed class CountingStorage : ISaveStorage
    {
        public readonly List<int> SavedStages = new();
        public SaveData LoadedData;
        public Exception LoadError;
        public UniTaskCompletionSource<bool> FirstSaveGate;
        public bool SaveResult = true;
        public int SaveCalls;
        public int DeleteCalls;

        public UniTask<SaveData> LoadAsync() => LoadError == null
            ? UniTask.FromResult(LoadedData)
            : UniTask.FromException<SaveData>(LoadError);

        public async UniTask<bool> SaveAsync(SaveData data)
        {
            SaveCalls++;
            SavedStages.Add(data.stage.highestClearedIndex);
            if (SaveCalls == 1 && FirstSaveGate != null) return await FirstSaveGate.Task;
            return SaveResult;
        }

        public UniTask DeleteAsync()
        {
            DeleteCalls++;
            return UniTask.CompletedTask;
        }
    }

    // 앱인토스 저장소와 같은 로더를 쓰되, 브릿지 응답(string 또는 Exception)을 큐로 대신한다.
    private sealed class FakeBridgeStorage : ISaveStorage
    {
        public readonly Queue<object> Responses = new();
        public readonly List<int> Timeouts = new();
        public int SaveCalls;
        public int DeleteCalls;

        public int GetCalls => Timeouts.Count;

        public UniTask<SaveData> LoadAsync() => AppsInTossSaveLoader.LoadAsync(GetItemAsync);

        public UniTask<bool> SaveAsync(SaveData data)
        {
            SaveCalls++;
            return UniTask.FromResult(true);
        }

        public UniTask DeleteAsync()
        {
            DeleteCalls++;
            return UniTask.CompletedTask;
        }

        private UniTask<string> GetItemAsync(int timeoutMs)
        {
            Timeouts.Add(timeoutMs);
            object response = Responses.Dequeue();
            return response is Exception error
                ? UniTask.FromException<string>(error)
                : UniTask.FromResult((string)response);
        }
    }
}
#endif
