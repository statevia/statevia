using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Statevia.Tools.Capacity;

/// <summary>容量ハーネスの CLI 入口。秘密は標準出力へ出さない。</summary>
internal static class CapacityCli
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private const string CompletedStatus = "Completed";
    private const string FailedStatus = "Failed";
    private const string CancelledStatus = "Cancelled";

    /// <summary>引数を解釈してシナリオを実行する。</summary>
    /// <param name="args">コマンドライン。</param>
    /// <returns>0 成功、1 用法、2 実行失敗。</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Any(static arg => arg is "-h" or "--help"))
        {
            PrintHelp();
            return 0;
        }

        CapacityCliOptions options;
        try
        {
            options = Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            PrintHelp();
            return 1;
        }

        using var cts = new CancellationTokenSource(options.Timeout);
        try
        {
            var measuredAt = DateTimeOffset.UtcNow;
            CapacityRunResult run;
            if (options.ScenarioId == "D1")
            {
                run = await RunD1Async(options, measuredAt, cts.Token).ConfigureAwait(false);
            }
            else
            {
                using var api = CreateApi(options, TimeSpan.FromSeconds(60));
                await api.EnsureHealthyAsync(cts.Token).ConfigureAwait(false);
                await AuthenticateAsync(api, options, cts.Token).ConfigureAwait(false);

                run = options.ScenarioId switch
                {
                    "L1" => await RunL1Async(
                            api,
                            options,
                            measuredAt,
                            ScenarioDefinitions.L1ResourceName,
                            "capacity-l1",
                            cts.Token)
                        .ConfigureAwait(false),
                    "L1O" => await RunL1Async(
                            api,
                            options,
                            measuredAt,
                            ScenarioDefinitions.L1OccupiedResourceName,
                            "capacity-l1o",
                            cts.Token)
                        .ConfigureAwait(false),
                    "L2" => await RunL2Async(api, options, measuredAt, cts.Token).ConfigureAwait(false),
                    "L3" => await RunL3Async(api, options, measuredAt, cts.Token).ConfigureAwait(false),
                    "C1" => await RunC1Async(api, options, measuredAt, cts.Token).ConfigureAwait(false),
                    _ => throw new ArgumentException($"Unknown scenario '{options.ScenarioId}'."),
                };
            }

            WriteOutputs(options.OutputPath, run);
            Console.Error.WriteLine($"Wrote {options.OutputPath}");
            if (run.ScenarioId == "D1"
                && run.Metrics.TryGetValue("d1Pass", out var d1Pass)
                && d1Pass < 1)
            {
                Console.Error.WriteLine("D1 did not pass.");
                return 2;
            }

            return 0;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static async Task AuthenticateAsync(CapacityApi api, CapacityCliOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey) || !string.IsNullOrWhiteSpace(options.AccessToken))
        {
            api.ApplyPrincipal(options.TenantKey, options.AccessToken, options.ApiKey);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
        {
            throw new ArgumentException("Specify --token, --api-key, or --username with STATEVIA_CAPACITY_PASSWORD / --password.");
        }

        await api.LoginAsync(options.TenantKey, options.Username, options.Password, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>短寿命または sleep 占有の Start → Completed 待ち。</summary>
    /// <param name="api">認証済み API。</param>
    /// <param name="options">CLI オプション。</param>
    /// <param name="measuredAt">計測開始時刻。</param>
    /// <param name="definitionResourceName">埋め込み YAML 名。</param>
    /// <param name="definitionNamePrefix">登録する Definition 名の接頭辞。</param>
    /// <param name="cancellationToken">打ち切り。</param>
    /// <returns>ハーネス結果。</returns>
    private static async Task<CapacityRunResult> RunL1Async(
        CapacityApi api,
        CapacityCliOptions options,
        DateTimeOffset measuredAt,
        string definitionResourceName,
        string definitionNamePrefix,
        CancellationToken cancellationToken)
    {
        var yaml = ScenarioDefinitions.ReadYaml(definitionResourceName);
        var definitionId = await api.CreateDefinitionAsync(
                $"{definitionNamePrefix}-{measuredAt.ToUnixTimeMilliseconds()}",
                yaml,
                cancellationToken)
            .ConfigureAwait(false);

        var wave = StopwatchStart();
        var starts = await StartManyAsync(api, definitionId, options, cancellationToken).ConfigureAwait(false);
        var accepted = starts.Where(static item => item.DisplayId is not null).Select(static item => item.DisplayId!).ToArray();
        var terminals = await WaitForTerminalAsync(api, accepted, options, cancellationToken).ConfigureAwait(false);
        wave.Stop();

        var completed = terminals.Count(static pair => pair.Value == CompletedStatus);
        var startLatencies = starts.Where(static item => item.DisplayId is not null).Select(static item => item.LatencyMilliseconds).ToArray();
        var http5xx = starts.Count(static item => item.Is5xx);
        var startErrors = starts.Count(static item => item.DisplayId is null);

        var metrics = BuildCommonMetrics(
            startAcceptCount: accepted.Length,
            startErrorCount: startErrors,
            http5xxCount: http5xx,
            completedCount: completed,
            failedCount: terminals.Count(static pair => pair.Value == FailedStatus),
            wallClock: wave.Elapsed,
            startLatencies: startLatencies);
        metrics["executionCountRequested"] = options.Count;

        return CreateResult(options, measuredAt, metrics, notes: $"definitionId={definitionId}");
    }

    private static async Task<CapacityRunResult> RunL2Async(
        CapacityApi api,
        CapacityCliOptions options,
        DateTimeOffset measuredAt,
        CancellationToken cancellationToken)
    {
        var yaml = ScenarioDefinitions.ReadYaml(ScenarioDefinitions.L2ResourceName);
        var definitionId = await api.CreateDefinitionAsync(
                $"capacity-l2-{measuredAt.ToUnixTimeMilliseconds()}",
                yaml,
                cancellationToken)
            .ConfigureAwait(false);

        var wave = StopwatchStart();
        var starts = await StartManyAsync(api, definitionId, options, cancellationToken).ConfigureAwait(false);
        var accepted = starts.Where(static item => item.DisplayId is not null).Select(static item => item.DisplayId!).ToArray();
        var discovered = await WaitForWaitsAsync(api, accepted, options, cancellationToken).ConfigureAwait(false);
        var resumeWave = await ResumeManyAsync(api, discovered.ToResume, options, cancellationToken).ConfigureAwait(false);
        var terminals = await WaitForTerminalAsync(api, accepted, options, cancellationToken).ConfigureAwait(false);
        wave.Stop();

        var resumeAttempts = resumeWave.Attempts;
        var skippedCompleted = discovered.SkippedCompletedCount + resumeWave.SkippedCompletedCount;
        var completed = terminals.Count(static pair => pair.Value == CompletedStatus);
        var startLatencies = starts.Where(static item => item.DisplayId is not null).Select(static item => item.LatencyMilliseconds).ToArray();
        var resumeLatencies = resumeAttempts.Where(static item => item.StatusCode is >= 200 and < 300).Select(static item => item.LatencyMilliseconds).ToArray();
        var resumeFailures = resumeAttempts.Count(static item => item.StatusCode is < 200 or >= 300);
        var http5xx = starts.Count(static item => item.Is5xx) + resumeAttempts.Count(static item => item.Is5xx);

        var metrics = BuildCommonMetrics(
            startAcceptCount: accepted.Length,
            startErrorCount: starts.Count(static item => item.DisplayId is null),
            http5xxCount: http5xx,
            completedCount: completed,
            failedCount: terminals.Count(static pair => pair.Value == FailedStatus),
            wallClock: wave.Elapsed,
            startLatencies: startLatencies);
        metrics["waitingCount"] = discovered.ToResume.Count;
        metrics["resumeSkippedCompletedCount"] = skippedCompleted;
        metrics["resumeAttemptedCount"] = resumeAttempts.Length;
        metrics["resumeAcceptCount"] = resumeAttempts.Length - resumeFailures;
        metrics["resumeErrorCount"] = resumeFailures;
        metrics["resumeRatePerSecond"] = LatencyStats.RatePerSecond(resumeAttempts.Length - resumeFailures, wave.Elapsed) ?? 0;
        PutPercentile(metrics, "resumeAcceptP50Ms", resumeLatencies, 50);
        PutPercentile(metrics, "resumeAcceptP95Ms", resumeLatencies, 95);
        metrics["executionCountRequested"] = options.Count;
        ResumeFailureSummary.ApplyToMetrics(metrics, resumeAttempts);

        if (skippedCompleted > 0)
        {
            Console.Error.WriteLine($"L2 skipped completed (no Resume HTTP): {skippedCompleted}");
        }

        var resumeSamples = ResumeFailureSummary.FormatSamples(resumeAttempts);
        if (!string.IsNullOrEmpty(resumeSamples))
        {
            Console.Error.WriteLine($"L2 resume errors: {resumeSamples}");
        }

        var notes = $"definitionId={definitionId}";
        if (skippedCompleted > 0)
        {
            notes += $"; resumeSkippedCompleted={skippedCompleted}";
        }

        if (!string.IsNullOrEmpty(resumeSamples))
        {
            notes += $"; resumeErrors={resumeSamples}";
        }

        return CreateResult(options, measuredAt, metrics, notes: notes);
    }

    private static async Task<CapacityRunResult> RunL3Async(
        CapacityApi api,
        CapacityCliOptions options,
        DateTimeOffset measuredAt,
        CancellationToken cancellationToken)
    {
        var yaml = ScenarioDefinitions.ReadYaml(ScenarioDefinitions.L3ResourceName);
        string definitionId;
        try
        {
            definitionId = await api.CreateDefinitionAsync(
                    $"capacity-l3-{measuredAt.ToUnixTimeMilliseconds()}",
                    yaml,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            var failedMetrics = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["delayWaitHttpAuthorable"] = 0,
                ["definitionCreateErrorCount"] = 1,
                ["autoCompletedCount"] = 0,
                ["stillWaitingCount"] = 0,
                ["executionCountRequested"] = options.Count,
            };
            return CreateResult(
                options,
                measuredAt,
                failedMetrics,
                notes: "POST /v1/definitions rejected L3 YAML. DelayWait is not HTTP-authorable.");
        }

        var wave = StopwatchStart();
        var starts = await StartManyAsync(api, definitionId, options, cancellationToken).ConfigureAwait(false);
        var accepted = starts.Where(static item => item.DisplayId is not null).Select(static item => item.DisplayId!).ToArray();
        var discovered = await WaitForWaitsAsync(api, accepted, options, cancellationToken).ConfigureAwait(false);
        var observeDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(ScenarioDefinitions.L3ObserveSecondsAfterWait);
        var autoTerminals = await WaitForTerminalUntilAsync(
                api,
                accepted,
                observeDeadline,
                options,
                cancellationToken)
            .ConfigureAwait(false);
        wave.Stop();

        var autoCompleted = autoTerminals.Count(static pair => pair.Value == CompletedStatus);
        var leftoverIds = accepted.Where(id => !autoTerminals.ContainsKey(id)).ToArray();
        var cancelStatuses = await CancelManyAsync(api, leftoverIds, options, cancellationToken).ConfigureAwait(false);
        var cancelledOk = cancelStatuses.Count(static code => code is >= 200 and < 300);

        var startLatencies = starts.Where(static item => item.DisplayId is not null).Select(static item => item.LatencyMilliseconds).ToArray();
        var metrics = BuildCommonMetrics(
            startAcceptCount: accepted.Length,
            startErrorCount: starts.Count(static item => item.DisplayId is null),
            http5xxCount: starts.Count(static item => item.Is5xx),
            completedCount: autoCompleted,
            failedCount: autoTerminals.Count(static pair => pair.Value == FailedStatus),
            wallClock: wave.Elapsed,
            startLatencies: startLatencies);
        metrics["waitingCount"] = discovered.ToResume.Count;
        metrics["autoCompletedCount"] = autoCompleted;
        metrics["stillWaitingCount"] = leftoverIds.Length;
        metrics["cancelledLeftoverCount"] = cancelledOk;
        metrics["delayWaitHttpAuthorable"] = autoCompleted > 0 ? 1 : 0;
        metrics["duplicateResumeCount"] = 0;
        metrics["executionCountRequested"] = options.Count;
        metrics["observeSecondsAfterWait"] = ScenarioDefinitions.L3ObserveSecondsAfterWait;
        metrics["yamlTimeoutSeconds"] = ScenarioDefinitions.L3YamlTimeout.TotalSeconds;

        var notes = autoCompleted > 0
            ? $"definitionId={definitionId}; TimerFire observed without HTTP resume"
            : $"definitionId={definitionId}; DelayWait did not fire over HTTP (wait.timeout unused; wait_kind always EventWait). Leftovers cancelled. poll floor 5s is config, not a measured TimerFire delay.";

        return CreateResult(options, measuredAt, metrics, notes: notes);
    }

    /// <summary>Wait 定義を Start し、短い settle のあと一斉 Cancel する。WAITING snapshot は待たない。</summary>
    private static async Task<CapacityRunResult> RunC1Async(
        CapacityApi api,
        CapacityCliOptions options,
        DateTimeOffset measuredAt,
        CancellationToken cancellationToken)
    {
        var yaml = ScenarioDefinitions.ReadYaml(ScenarioDefinitions.L2ResourceName);
        var definitionId = await api.CreateDefinitionAsync(
                $"capacity-c1-{measuredAt.ToUnixTimeMilliseconds()}",
                yaml,
                cancellationToken)
            .ConfigureAwait(false);

        var wave = StopwatchStart();
        var starts = await StartManyAsync(api, definitionId, options, cancellationToken).ConfigureAwait(false);
        var accepted = starts.Where(static item => item.DisplayId is not null).Select(static item => item.DisplayId!).ToArray();
        await Task.Delay(options.SettleDelay, cancellationToken).ConfigureAwait(false);

        var toCancel = new List<string>();
        var completedBeforeCancel = 0;
        foreach (var id in accepted)
        {
            var status = await api.GetExecutionStatusAsync(id, cancellationToken).ConfigureAwait(false);
            if (status == CompletedStatus)
            {
                completedBeforeCancel++;
                continue;
            }

            if (status is FailedStatus or CancelledStatus)
            {
                continue;
            }

            toCancel.Add(id);
        }

        var cancelAttempts = await CancelManyAttemptsAsync(api, toCancel, options, cancellationToken).ConfigureAwait(false);
        var terminals = await WaitForTerminalAsync(api, accepted, options, cancellationToken).ConfigureAwait(false);
        wave.Stop();

        var cancelOk = cancelAttempts.Count(static item => item.StatusCode is >= 200 and < 300);
        var cancelFail = cancelAttempts.Length - cancelOk;
        var cancelled = terminals.Count(static pair => pair.Value == CancelledStatus);
        var startLatencies = starts.Where(static item => item.DisplayId is not null).Select(static item => item.LatencyMilliseconds).ToArray();
        var cancelLatencies = cancelAttempts.Where(static item => item.StatusCode is >= 200 and < 300).Select(static item => item.LatencyMilliseconds).ToArray();
        var http5xx = starts.Count(static item => item.Is5xx) + cancelAttempts.Count(static item => item.Is5xx);

        var metrics = BuildCommonMetrics(
            startAcceptCount: accepted.Length,
            startErrorCount: starts.Count(static item => item.DisplayId is null),
            http5xxCount: http5xx,
            completedCount: terminals.Count(static pair => pair.Value == CompletedStatus),
            failedCount: terminals.Count(static pair => pair.Value == FailedStatus),
            wallClock: wave.Elapsed,
            startLatencies: startLatencies);
        metrics["cancelledCount"] = cancelled;
        metrics["completedBeforeCancelCount"] = completedBeforeCancel;
        metrics["cancelAttemptedCount"] = cancelAttempts.Length;
        metrics["cancelAcceptCount"] = cancelOk;
        metrics["cancelErrorCount"] = cancelFail;
        metrics["cancelRatePerSecond"] = LatencyStats.RatePerSecond(cancelOk, wave.Elapsed) ?? 0;
        metrics["cancelledRatePerSecond"] = LatencyStats.RatePerSecond(cancelled, wave.Elapsed) ?? 0;
        metrics["executionCountRequested"] = options.Count;
        metrics["settleDelayMs"] = options.SettleDelay.TotalMilliseconds;
        PutPercentile(metrics, "cancelAcceptP50Ms", cancelLatencies, 50);
        PutPercentile(metrics, "cancelAcceptP95Ms", cancelLatencies, 95);

        return CreateResult(
            options,
            measuredAt,
            metrics,
            notes: $"definitionId={definitionId}; cancelAccept={cancelOk}; cancelled={cancelled}");
    }

    private static async Task<CapacityRunResult> RunD1Async(
        CapacityCliOptions options,
        DateTimeOffset measuredAt,
        CancellationToken cancellationToken)
    {
        var gitRoot = FindGitRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
        var restartSucceeded = false;
        var healthRestored = false;
        string[] accepted = [];
        CapacityApi api = CreateApi(options, TimeSpan.FromSeconds(60));
        try
        {
            await ComposeRestart.ProbeAsync(gitRoot, cancellationToken).ConfigureAwait(false);
            await api.EnsureHealthyAsync(cancellationToken).ConfigureAwait(false);
            await AuthenticateAsync(api, options, cancellationToken).ConfigureAwait(false);

            var yaml = ScenarioDefinitions.ReadYaml(ScenarioDefinitions.L2ResourceName);
            var definitionId = await api.CreateDefinitionAsync(
                    $"capacity-d1-{measuredAt.ToUnixTimeMilliseconds()}",
                    yaml,
                    cancellationToken)
                .ConfigureAwait(false);

            var wave = StopwatchStart();
            var starts = await StartManyAsync(api, definitionId, options, cancellationToken).ConfigureAwait(false);
            accepted = starts.Where(static item => item.DisplayId is not null).Select(static item => item.DisplayId!).ToArray();
            var discoveredBefore = await WaitForWaitsAsync(api, accepted, options, cancellationToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

            await ComposeRestart.RestartAsync(gitRoot, options.RestartServiceName, cancellationToken).ConfigureAwait(false);
            restartSucceeded = true;

            api.Dispose();
            api = CreateApi(options, TimeSpan.FromSeconds(5));
            await WaitUntilHealthyAsync(api, cancellationToken).ConfigureAwait(false);
            healthRestored = true;
            api.Dispose();
            api = CreateApi(options, TimeSpan.FromSeconds(60));
            await AuthenticateAsync(api, options, cancellationToken).ConfigureAwait(false);

            var projectionErrorCount = await CountProjectionErrorsAsync(api, accepted, cancellationToken).ConfigureAwait(false);
            var discoveredAfter = await DiscoverWaitsSnapshotAsync(api, accepted, cancellationToken).ConfigureAwait(false);
            var resumeWave = await ResumeManyAsync(api, discoveredAfter, options, cancellationToken).ConfigureAwait(false);
            var terminals = await WaitForTerminalAsync(api, accepted, options, cancellationToken).ConfigureAwait(false);
            wave.Stop();

            var resumeAttempts = resumeWave.Attempts;
            var resumeFailures = resumeAttempts.Count(static item => item.StatusCode is < 200 or >= 300);
            var completed = terminals.Count(static pair => pair.Value == CompletedStatus);
            var failed = terminals.Count(static pair => pair.Value == FailedStatus);
            var startLatencies = starts.Where(static item => item.DisplayId is not null).Select(static item => item.LatencyMilliseconds).ToArray();
            var resumeLatencies = resumeAttempts.Where(static item => item.StatusCode is >= 200 and < 300).Select(static item => item.LatencyMilliseconds).ToArray();
            var http5xx = starts.Count(static item => item.Is5xx) + resumeAttempts.Count(static item => item.Is5xx);
            var d1Pass = D1PassEvaluator.IsPass(
                restartSucceeded,
                healthRestored,
                projectionErrorCount,
                resumeFailures,
                completed,
                accepted.Length,
                failed);

            var metrics = BuildCommonMetrics(
                startAcceptCount: accepted.Length,
                startErrorCount: starts.Count(static item => item.DisplayId is null),
                http5xxCount: http5xx,
                completedCount: completed,
                failedCount: failed,
                wallClock: wave.Elapsed,
                startLatencies: startLatencies);
            metrics["waitingCountBeforeRestart"] = discoveredBefore.ToResume.Count;
            metrics["waitingCountAfterRestart"] = discoveredAfter.Length;
            metrics["projectionErrorCount"] = projectionErrorCount;
            metrics["restartSucceeded"] = restartSucceeded ? 1 : 0;
            metrics["healthRestored"] = healthRestored ? 1 : 0;
            metrics["d1Pass"] = d1Pass ? 1 : 0;
            metrics["resumeAttemptedCount"] = resumeAttempts.Length;
            metrics["resumeAcceptCount"] = resumeAttempts.Length - resumeFailures;
            metrics["resumeErrorCount"] = resumeFailures;
            metrics["resumeSkippedCompletedCount"] = resumeWave.SkippedCompletedCount;
            PutPercentile(metrics, "resumeAcceptP50Ms", resumeLatencies, 50);
            PutPercentile(metrics, "resumeAcceptP95Ms", resumeLatencies, 95);
            metrics["executionCountRequested"] = options.Count;
            ResumeFailureSummary.ApplyToMetrics(metrics, resumeAttempts);

            return CreateResult(
                options,
                measuredAt,
                metrics,
                notes: $"definitionId={definitionId}; restartService={options.RestartServiceName}; d1Pass={d1Pass}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            if (accepted.Length > 0)
            {
                try
                {
                    await CancelManyAsync(api, accepted, options, CancellationToken.None).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    Console.Error.WriteLine("D1 leftover cancel failed.");
                }
            }

            var metrics = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["restartSucceeded"] = restartSucceeded ? 1 : 0,
                ["healthRestored"] = healthRestored ? 1 : 0,
                ["d1Pass"] = 0,
                ["executionCountRequested"] = options.Count,
            };
            return CreateResult(options, measuredAt, metrics, notes: exception.Message);
        }
        finally
        {
            api.Dispose();
        }
    }

    private static async Task<CapacityApi.StartAttempt[]> StartManyAsync(
        CapacityApi api,
        string definitionId,
        CapacityCliOptions options,
        CancellationToken cancellationToken)
    {
        var results = new CapacityApi.StartAttempt[options.Count];
        await Parallel.ForEachAsync(
                Enumerable.Range(0, options.Count),
                new ParallelOptions { MaxDegreeOfParallelism = options.Concurrency, CancellationToken = cancellationToken },
                async (index, token) =>
                {
                    results[index] = await api.StartExecutionAsync(definitionId, token).ConfigureAwait(false);
                })
            .ConfigureAwait(false);
        return results;
    }

    private static async Task<Dictionary<string, string>> WaitForTerminalAsync(
        CapacityApi api,
        IReadOnlyList<string> executionIds,
        CapacityCliOptions options,
        CancellationToken cancellationToken)
    {
        var terminals = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = executionIds.ToHashSet(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var id in pending.ToArray())
            {
                var status = await api.GetExecutionStatusAsync(id, cancellationToken).ConfigureAwait(false);
                if (status is CompletedStatus or FailedStatus or CancelledStatus)
                {
                    terminals[id] = status;
                    pending.Remove(id);
                }
            }

            if (pending.Count > 0)
            {
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        return terminals;
    }

    private static async Task<L2WaitDiscoverResult> WaitForWaitsAsync(
        CapacityApi api,
        IReadOnlyList<string> executionIds,
        CapacityCliOptions options,
        CancellationToken cancellationToken)
    {
        var found = new ConcurrentDictionary<string, (string NodeId, string ResumeKey)>(StringComparer.Ordinal);
        var pending = executionIds.ToHashSet(StringComparer.Ordinal);
        var skippedCompleted = 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var id in pending.ToArray())
            {
                var status = await api.GetExecutionStatusAsync(id, cancellationToken).ConfigureAwait(false);
                if (L2ResumeGate.IsCompleted(status))
                {
                    skippedCompleted++;
                    pending.Remove(id);
                    continue;
                }

                if (L2ResumeGate.IsFailedOrCancelled(status))
                {
                    pending.Remove(id);
                    continue;
                }

                var waits = await api.GetWaitsAsync(id, cancellationToken).ConfigureAwait(false);
                var match = waits.FirstOrDefault(static wait =>
                    wait.AllowedEvents.Contains(ScenarioDefinitions.L2ResumeEvent, StringComparer.Ordinal));
                if (match is not null)
                {
                    found[id] = (match.NodeId, ScenarioDefinitions.L2ResumeEvent);
                    pending.Remove(id);
                }
            }

            if (pending.Count > 0)
            {
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        var toResume = found.Select(static pair => (pair.Key, pair.Value.NodeId, pair.Value.ResumeKey)).ToArray();
        return new L2WaitDiscoverResult(toResume, skippedCompleted);
    }

    private static async Task<L2ResumeWaveResult> ResumeManyAsync(
        CapacityApi api,
        IReadOnlyList<(string ExecutionId, string NodeId, string ResumeKey)> waiting,
        CapacityCliOptions options,
        CancellationToken cancellationToken)
    {
        var attempts = new ConcurrentBag<CapacityApi.ResumeAttempt>();
        var skippedCompleted = 0;
        await Parallel.ForEachAsync(
                waiting,
                new ParallelOptions { MaxDegreeOfParallelism = options.Concurrency, CancellationToken = cancellationToken },
                async (item, token) =>
                {
                    var status = await api.GetExecutionStatusAsync(item.ExecutionId, token).ConfigureAwait(false);
                    if (L2ResumeGate.ShouldSkipResume(status))
                    {
                        if (L2ResumeGate.IsCompleted(status))
                        {
                            Interlocked.Increment(ref skippedCompleted);
                        }

                        return;
                    }

                    var attempt = await api.ResumeNodeAsync(item.ExecutionId, item.NodeId, item.ResumeKey, token)
                        .ConfigureAwait(false);
                    if (attempt.Is5xx)
                    {
                        await Task.Delay(options.PollInterval, token).ConfigureAwait(false);
                        attempt = await api.ResumeNodeAsync(item.ExecutionId, item.NodeId, item.ResumeKey, token)
                            .ConfigureAwait(false);
                    }

                    attempts.Add(attempt);
                })
            .ConfigureAwait(false);
        return new L2ResumeWaveResult(attempts.ToArray(), skippedCompleted);
    }

    private static async Task<Dictionary<string, string>> WaitForTerminalUntilAsync(
        CapacityApi api,
        IReadOnlyList<string> executionIds,
        DateTimeOffset deadlineUtc,
        CapacityCliOptions options,
        CancellationToken cancellationToken)
    {
        var terminals = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = executionIds.ToHashSet(StringComparer.Ordinal);
        while (pending.Count > 0 && DateTimeOffset.UtcNow < deadlineUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var id in pending.ToArray())
            {
                var status = await api.GetExecutionStatusAsync(id, cancellationToken).ConfigureAwait(false);
                if (status is CompletedStatus or FailedStatus or CancelledStatus)
                {
                    terminals[id] = status;
                    pending.Remove(id);
                }
            }

            if (pending.Count > 0 && DateTimeOffset.UtcNow < deadlineUtc)
            {
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        return terminals;
    }

    private static async Task<int[]> CancelManyAsync(
        CapacityApi api,
        IReadOnlyList<string> executionIds,
        CapacityCliOptions options,
        CancellationToken cancellationToken)
    {
        var results = new int[executionIds.Count];
        await Parallel.ForEachAsync(
                Enumerable.Range(0, executionIds.Count),
                new ParallelOptions { MaxDegreeOfParallelism = options.Concurrency, CancellationToken = cancellationToken },
                async (index, token) =>
                {
                    results[index] = (await api.CancelExecutionAsync(executionIds[index], token).ConfigureAwait(false)).StatusCode;
                })
            .ConfigureAwait(false);
        return results;
    }

    /// <summary>複数実行を並列 Cancel し、HTTP 結果を返す。</summary>
    private static async Task<CapacityApi.CancelAttempt[]> CancelManyAttemptsAsync(
        CapacityApi api,
        IReadOnlyList<string> executionIds,
        CapacityCliOptions options,
        CancellationToken cancellationToken)
    {
        var results = new CapacityApi.CancelAttempt[executionIds.Count];
        await Parallel.ForEachAsync(
                Enumerable.Range(0, executionIds.Count),
                new ParallelOptions { MaxDegreeOfParallelism = options.Concurrency, CancellationToken = cancellationToken },
                async (index, token) =>
                {
                    results[index] = await api.CancelExecutionAsync(executionIds[index], token).ConfigureAwait(false);
                })
            .ConfigureAwait(false);
        return results;
    }

    private static async Task<(string ExecutionId, string NodeId, string ResumeKey)[]> DiscoverWaitsSnapshotAsync(
        CapacityApi api,
        IReadOnlyList<string> executionIds,
        CancellationToken cancellationToken)
    {
        var found = new List<(string ExecutionId, string NodeId, string ResumeKey)>();
        foreach (var id in executionIds)
        {
            var status = await api.GetExecutionStatusAsync(id, cancellationToken).ConfigureAwait(false);
            if (L2ResumeGate.ShouldSkipResume(status))
            {
                continue;
            }

            var waits = await api.GetWaitsAsync(id, cancellationToken).ConfigureAwait(false);
            var match = waits.FirstOrDefault(static wait =>
                wait.AllowedEvents.Contains(ScenarioDefinitions.L2ResumeEvent, StringComparer.Ordinal));
            if (match is not null)
            {
                found.Add((id, match.NodeId, ScenarioDefinitions.L2ResumeEvent));
            }
        }

        return found.ToArray();
    }

    private static async Task<int> CountProjectionErrorsAsync(
        CapacityApi api,
        IReadOnlyList<string> executionIds,
        CancellationToken cancellationToken)
    {
        var errors = 0;
        foreach (var id in executionIds)
        {
            try
            {
                var status = await api.GetExecutionStatusAsync(id, cancellationToken).ConfigureAwait(false);
                if (status is null || status == FailedStatus || status == CancelledStatus)
                {
                    errors++;
                    continue;
                }

                var graphCode = await api.GetGraphStatusCodeAsync(id, cancellationToken).ConfigureAwait(false);
                if (graphCode is < 200 or >= 300)
                {
                    errors++;
                    continue;
                }

                var waits = await api.GetWaitsAsync(id, cancellationToken).ConfigureAwait(false);
                if (status != CompletedStatus && waits.Count == 0)
                {
                    errors++;
                }
            }
            catch (HttpRequestException)
            {
                errors++;
            }
        }

        return errors;
    }

    private static async Task WaitUntilHealthyAsync(CapacityApi api, CancellationToken cancellationToken)
    {
        const int maxAttempts = 45;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await api.TryEnsureHealthyAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        throw new HttpRequestException("Service API did not become healthy after restart.");
    }

    private static CapacityApi CreateApi(CapacityCliOptions options, TimeSpan requestTimeout)
    {
        return new CapacityApi(options.BaseUrl, requestTimeout);
    }

    private static Dictionary<string, double> BuildCommonMetrics(
        int startAcceptCount,
        int startErrorCount,
        int http5xxCount,
        int completedCount,
        int failedCount,
        TimeSpan wallClock,
        IReadOnlyList<double> startLatencies)
    {
        var metrics = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["startAcceptCount"] = startAcceptCount,
            ["startErrorCount"] = startErrorCount,
            ["http5xxCount"] = http5xxCount,
            ["completedCount"] = completedCount,
            ["failedCount"] = failedCount,
            ["wallClockSeconds"] = wallClock.TotalSeconds,
            ["startAcceptRatePerSecond"] = LatencyStats.RatePerSecond(startAcceptCount, wallClock) ?? 0,
            ["completedRatePerSecond"] = LatencyStats.RatePerSecond(completedCount, wallClock) ?? 0,
        };
        PutPercentile(metrics, "startAcceptP50Ms", startLatencies, 50);
        PutPercentile(metrics, "startAcceptP95Ms", startLatencies, 95);
        return metrics;
    }

    private static void PutPercentile(IDictionary<string, double> metrics, string key, IReadOnlyList<double> samples, double percentile)
    {
        var value = LatencyStats.PercentileNearestRank(samples, percentile);
        if (value is not null)
        {
            metrics[key] = value.Value;
        }
    }

    private static CapacityRunResult CreateResult(
        CapacityCliOptions options,
        DateTimeOffset measuredAt,
        IReadOnlyDictionary<string, double> metrics,
        string notes)
    {
        var gitRoot = FindGitRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
        var stamped = new Dictionary<string, double>(metrics, StringComparer.Ordinal);
        if (options.WorkerReplicas > 0)
        {
            stamped["workerReplicas"] = options.WorkerReplicas;
        }

        if (options.WorkerMaxConcurrency > 0)
        {
            stamped["workerMaxConcurrency"] = options.WorkerMaxConcurrency;
        }

        if (options.WorkerCancelConcurrency > 0)
        {
            stamped["workerCancelConcurrency"] = options.WorkerCancelConcurrency;
        }

        return new CapacityRunResult
        {
            ScenarioId = options.ScenarioId,
            MeasuredAtUtc = measuredAt,
            GitSha = GitShaReader.TryRead(gitRoot),
            HwLabel = options.HwLabel,
            HwNotes = options.HwNotes,
            Topology = options.Topology,
            Metrics = stamped,
            ProvisionalCap = new Dictionary<string, double>(StringComparer.Ordinal),
            Notes = notes,
        };
    }

    private static void WriteOutputs(string jsonPath, CapacityRunResult result)
    {
        var directory = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(result, JsonWriteOptions);
        File.WriteAllText(jsonPath, json, Encoding.UTF8);

        var csvPath = Path.ChangeExtension(jsonPath, ".csv");
        var csv = new StringBuilder();
        csv.AppendLine("metric,value");
        csv.AppendLine(CultureInfo.InvariantCulture, $"scenarioId,{EscapeCsv(result.ScenarioId)}");
        csv.AppendLine(CultureInfo.InvariantCulture, $"measuredAtUtc,{EscapeCsv(result.MeasuredAtUtc.ToString("o", CultureInfo.InvariantCulture))}");
        csv.AppendLine(CultureInfo.InvariantCulture, $"gitSha,{EscapeCsv(result.GitSha)}");
        csv.AppendLine(CultureInfo.InvariantCulture, $"hwLabel,{EscapeCsv(result.HwLabel)}");
        csv.AppendLine(CultureInfo.InvariantCulture, $"hwNotes,{EscapeCsv(result.HwNotes)}");
        csv.AppendLine(CultureInfo.InvariantCulture, $"topology,{EscapeCsv(result.Topology)}");
        csv.AppendLine(CultureInfo.InvariantCulture, $"notes,{EscapeCsv(result.Notes)}");
        foreach (var pair in result.Metrics.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            csv.AppendLine(CultureInfo.InvariantCulture, $"{pair.Key},{pair.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private static CapacityCliOptions Parse(string[] args)
    {
        string? scenario = null;
        var baseUrl = "http://localhost:8080";
        var tenant = "default";
        string? username = "admin";
        string? password = Environment.GetEnvironmentVariable("STATEVIA_CAPACITY_PASSWORD");
        string? token = null;
        string? apiKey = null;
        var count = 8;
        var concurrency = 2;
        var pollMs = 250;
        var timeoutSeconds = 180;
        var timeoutSpecified = false;
        string? output = null;
        var hwLabel = "ref-dev";
        var hwNotes = string.Empty;
        var topology = "phase0-single-api";
        var restartService = ComposeRestart.DefaultServiceName;
        var workerReplicas = 0;
        var workerMaxConcurrency = 0;
        var workerCancelConcurrency = 0;
        var settleMs = 3000;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            string ReadValue()
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}.");
                }

                index++;
                return args[index];
            }

            switch (arg)
            {
                case "--scenario":
                    scenario = ReadValue();
                    break;
                case "--base-url":
                    baseUrl = ReadValue();
                    break;
                case "--tenant":
                    tenant = ReadValue();
                    break;
                case "--username":
                    username = ReadValue();
                    break;
                case "--password":
                    password = ReadValue();
                    break;
                case "--token":
                    token = ReadValue();
                    break;
                case "--api-key":
                    apiKey = ReadValue();
                    break;
                case "--count":
                    count = ParsePositiveInt(ReadValue(), "--count");
                    break;
                case "--concurrency":
                    concurrency = ParsePositiveInt(ReadValue(), "--concurrency");
                    break;
                case "--poll-ms":
                    pollMs = ParsePositiveInt(ReadValue(), "--poll-ms");
                    break;
                case "--timeout-seconds":
                    timeoutSeconds = ParsePositiveInt(ReadValue(), "--timeout-seconds");
                    timeoutSpecified = true;
                    break;
                case "--output":
                    output = ReadValue();
                    break;
                case "--hw-label":
                    hwLabel = ReadValue();
                    break;
                case "--hw-notes":
                    hwNotes = ReadValue();
                    break;
                case "--topology":
                    topology = ReadValue();
                    break;
                case "--restart-service":
                    restartService = ComposeRestart.NormalizeServiceName(ReadValue());
                    break;
                case "--worker-replicas":
                    workerReplicas = ParseNonNegativeInt(ReadValue(), "--worker-replicas");
                    break;
                case "--worker-max-concurrency":
                    workerMaxConcurrency = ParseNonNegativeInt(ReadValue(), "--worker-max-concurrency");
                    break;
                case "--worker-cancel-concurrency":
                    workerCancelConcurrency = ParseNonNegativeInt(ReadValue(), "--worker-cancel-concurrency");
                    break;
                case "--settle-ms":
                    settleMs = ParseNonNegativeInt(ReadValue(), "--settle-ms");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(scenario) || scenario is not ("L1" or "L1O" or "L2" or "L3" or "D1" or "C1"))
        {
            throw new ArgumentException("--scenario must be L1, L1O, L2, L3, D1, or C1.");
        }

        if ((scenario is "D1" or "C1" or "L1O") && !timeoutSpecified)
        {
            timeoutSeconds = 300;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException("--base-url must be an absolute URI.");
        }

        output ??= Path.Combine("tools", "capacity", "results", $"{scenario.ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");

        if (!string.IsNullOrWhiteSpace(token) || !string.IsNullOrWhiteSpace(apiKey))
        {
            username = null;
            password = null;
        }

        return new CapacityCliOptions
        {
            ScenarioId = scenario,
            BaseUrl = baseUri,
            TenantKey = tenant,
            Username = username,
            Password = password,
            AccessToken = token,
            ApiKey = apiKey,
            Count = count,
            Concurrency = concurrency,
            PollInterval = TimeSpan.FromMilliseconds(pollMs),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            OutputPath = output,
            HwLabel = hwLabel,
            HwNotes = hwNotes,
            Topology = topology,
            RestartServiceName = restartService,
            WorkerReplicas = workerReplicas,
            WorkerMaxConcurrency = workerMaxConcurrency,
            WorkerCancelConcurrency = workerCancelConcurrency,
            SettleDelay = TimeSpan.FromMilliseconds(settleMs),
        };
    }

    private static int ParseNonNegativeInt(string raw, string name)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw new ArgumentException($"{name} must be a non-negative integer.");
        }

        return value;
    }

    private static int ParsePositiveInt(string raw, string name)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new ArgumentException($"{name} must be a positive integer.");
        }

        return value;
    }

    private static string? FindGitRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static System.Diagnostics.Stopwatch StopwatchStart()
    {
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        return watch;
    }

    private static void PrintHelp()
    {
        Console.Error.WriteLine("Statevia capacity harness (L1 / L1O / L2 / L3 / D1 / C1). Do not pass secrets to stdout.");
        Console.Error.WriteLine("  --scenario L1|L1O|L2|L3|D1|C1");
        Console.Error.WriteLine("  --base-url http://localhost:8080");
        Console.Error.WriteLine("  --tenant default");
        Console.Error.WriteLine("  --username admin   (or --token / --api-key)");
        Console.Error.WriteLine("  STATEVIA_CAPACITY_PASSWORD or --password");
        Console.Error.WriteLine("  --count 8 --concurrency 2 --output path.json");
        Console.Error.WriteLine("  --hw-label ref-dev --hw-notes \"vCPU/memory\" --topology phase0-single-api");
        Console.Error.WriteLine("  D1: --restart-service service-api (docker compose restart). Default timeout 300s.");
        Console.Error.WriteLine("  L3: probes DelayWait via wait.timeout; current Loader does not author DelayWait over HTTP.");
        Console.Error.WriteLine("  L1O: Start builtin sleep 500ms defs and wait for Completed. Default timeout 300s.");
        Console.Error.WriteLine("  C1: Start wait defs, settle, Cancel. Does not wait for WAITING snapshot. Default timeout 300s.");
        Console.Error.WriteLine("  Record-only: --worker-replicas --worker-max-concurrency --worker-cancel-concurrency --settle-ms");
    }

    /// <summary>Wait 発見結果。終端済みは Resume リストに入れない。</summary>
    /// <param name="ToResume">まだ待ち中で Resume する実行。</param>
    /// <param name="SkippedCompletedCount">ポーリング中に Completed だった件数。</param>
    private sealed record L2WaitDiscoverResult(
        IReadOnlyList<(string ExecutionId, string NodeId, string ResumeKey)> ToResume,
        int SkippedCompletedCount);

    /// <summary>Resume ウェーブ。送らなかった Completed は Attempts に含めない。</summary>
    /// <param name="Attempts">実際に送った Resume HTTP。</param>
    /// <param name="SkippedCompletedCount">直前再確認で Completed のため送らなかった件数。</param>
    private sealed record L2ResumeWaveResult(CapacityApi.ResumeAttempt[] Attempts, int SkippedCompletedCount);
}
