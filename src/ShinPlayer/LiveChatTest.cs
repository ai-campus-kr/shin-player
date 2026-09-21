using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShinPlayer;

// Explicit opt-in only. Ordinary integration tests never read .env or call the paid API.
internal static class LiveChatTest
{
    internal static string ReadKey(string envPath)
    {
        // Explicit diagnostic opt-in only; ordinary tests never read this store.
        if (envPath == "--saved-key")
        {
            var saved = new ApiKeyStore(PlayerSettings.DirectoryPath).Load();
            if (saved.Length == 0) throw new InvalidOperationException("No readable key is saved for this Windows user.");
            return saved;
        }
        var line = File.ReadLines(envPath).FirstOrDefault(x => x.TrimStart().StartsWith("OPENAI_API_KEY=", StringComparison.Ordinal));
        var key = line?.Trim().Split('=', 2)[1].Trim().Trim('"', '\'') ?? "";
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("The selected .env has no OPENAI_API_KEY.");
        return key;
    }

    internal static async Task<int> RunAsync(string envPath, string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<object>();
        try
        {
            string key = ReadKey(envPath);
            var client = new VideoChatClient();
            var transcript = new VideoTranscript("synthetic-api-test", "API 설정 강의 · 합성 테스트 자막", new[]
            {
                new SubtitleCue(0, 9, "오늘은 신플레이어의 API 설정 방법을 설명합니다."),
                new SubtitleCue(10, 19, "오른쪽 채팅 패널에서 API 설정 창을 열고 본인의 OpenAI API 키를 입력한 다음 키 저장을 누르세요."),
                new SubtitleCue(20, 29, "API 키는 Windows 사용자 계정으로 암호화되어 저장됩니다. 모델은 gpt-5.4-mini입니다."),
                new SubtitleCue(30, 39, "자막 불러오기를 누르고 질문을 보내면 답변을 볼 수 있습니다.")
            });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(150));
            string previousQuestion = "", previousAnswer = "";
            foreach (var (question, shouldSeek) in new[] { ("API 키는 어디에서 입력하고 저장하나요?", false), ("방금 설명한 API 키 입력 부분으로 이동해줘", true), ("이동하지 말고 API 키가 어떻게 저장되는지만 설명해줘", false) })
            {
                var reply = await client.AskAsync(key, transcript, question, previousQuestion, timeout.Token, previousAnswer);
                bool passed = reply.SeekRequested == shouldSeek && reply.Matches.Count > 0 && (!shouldSeek || reply.Matches[0].Start == 10);
                results.Add(new { question, passed, reply.Answer, reply.SeekRequested, times = reply.Matches.Select(x => x.Start), reply.InputTokens, reply.OutputTokens });
                if (!passed) throw new InvalidOperationException("Live API did not preserve answer/seek intent or subtitle evidence.");
                previousQuestion = question; previousAnswer = reply.Answer;
            }
            await File.WriteAllTextAsync(Path.Combine(output, "live-api.json"), JsonSerializer.Serialize(new { passed = true, model = VideoChatClient.Model, syntheticTranscript = true, tests = results }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            // Do not include request objects, exception stacks, headers, or the credential.
            string error = ex is InvalidOperationException ? ex.Message : "Live API test failed: " + ex.GetType().Name;
            await File.WriteAllTextAsync(Path.Combine(output, "live-api.json"), JsonSerializer.Serialize(new { passed = false, error, tests = results }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
    }
}
